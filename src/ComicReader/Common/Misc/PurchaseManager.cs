// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReader.Common.Constants;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;

using Windows.Services.Store;

namespace ComicReader.Common.Misc;

internal static class PurchaseManager
{
    private const string TAG = nameof(PurchaseManager);
    private const string KEY_DONOR_TOKEN = "DonorToken";
    private const string ITEM_NAME_DONOR = "Donor";
    private const string STORE_ID_DONOR = "DonationUser1";

    private static bool? _isDonor;
    public static bool IsDonor
    {
        get
        {
            if (!_isDonor.HasValue)
            {
                string? token = KVStore.App.GetCollection(DatabaseEntry.KV_LIB_PURCHASES).GetValue<string>(KEY_DONOR_TOKEN);
                PurchaseInfo? info = VerifyPurchaseToken(token);
                _isDonor = info is not null && info.Name == ITEM_NAME_DONOR;
            }

            return _isDonor.Value;
        }
        private set
        {
            _isDonor = value;
            if (value)
            {
                PurchaseInfo info = new()
                {
                    Name = ITEM_NAME_DONOR,
                    PurchaseDate = DateTime.UtcNow,
                };
                string token = CreatePurchaseToken(info);
                KVStore.App.GetCollection(DatabaseEntry.KV_LIB_PURCHASES).Set(KEY_DONOR_TOKEN, token);
            }
            else
            {
                KVStore.App.GetCollection(DatabaseEntry.KV_LIB_PURCHASES).Set<string>(KEY_DONOR_TOKEN, null);
            }
        }
    }

    private static StoreContext? _context;
    private static StoreContext Context
    {
        get
        {
            StoreContext? context = _context;
            if (context is null)
            {
                context = StoreContext.GetDefault();
                _context = context;
            }

            return context;
        }
    }

    public static void ResetPurchaseStatus()
    {
        IsDonor = false;
    }

    public static async Task<OperationResult> UpdatePurchaseStatus()
    {
        StoreProductQueryResult result = await Context.GetUserCollectionAsync(["Durable"]);
        if (result.ExtendedError is not null)
        {
            return OperationResult.From(false, result.ExtendedError);
        }

        foreach (KeyValuePair<string, StoreProduct> pair in result.Products)
        {
            string storeId = pair.Key;
            switch (storeId)
            {
                case STORE_ID_DONOR:
                    IsDonor = true;
                    break;
                default:
                    break;
            }
        }

        return OperationResult.From(true, null);
    }

    public static async Task<OperationResult> PurchaseDonor()
    {
        StorePurchaseResult result = await Context.RequestPurchaseAsync(STORE_ID_DONOR);
        bool successful = result.Status switch
        {
            StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased => true,
            StorePurchaseStatus.NotPurchased or StorePurchaseStatus.NetworkError or StorePurchaseStatus.ServerError => false,
            _ => false,
        };
        return OperationResult.From(successful, result.ExtendedError);
    }

    private static string CreatePurchaseToken(PurchaseInfo info)
    {
        string purchaseInfoJson = JsonSerializer.Serialize(info.ToJsonModel());
        string deviceId = EnvironmentProvider.Instance.GetActualDeviceId();
        string payloadStr = $"{deviceId}|{purchaseInfoJson}";
        byte[] payload = Encoding.UTF8.GetBytes(payloadStr);

        using var rsa = RSA.Create(2048);
        byte[] publicKey = rsa.ExportSubjectPublicKeyInfo();
        byte[] privateKey = rsa.ExportPkcs8PrivateKey();
        byte[] signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        PurchaseTokenJsonModel token = new()
        {
            Signature = Convert.ToBase64String(signature),
            PublicKey = Convert.ToBase64String(publicKey),
            PurchaseInfoJson = purchaseInfoJson,
        };
        return JsonSerializer.Serialize(token);
    }

    private static PurchaseInfo? VerifyPurchaseToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        PurchaseTokenJsonModel? tokenObject;
        try
        {
            tokenObject = JsonSerializer.Deserialize<PurchaseTokenJsonModel>(token);
        }
        catch (JsonException ex)
        {
            Logger.E(TAG, ex);
            return null;
        }

        if (tokenObject is null)
        {
            return null;
        }

        string? signatureStr = tokenObject.Signature;
        string? publicKeyStr = tokenObject.PublicKey;
        if (string.IsNullOrEmpty(signatureStr) || string.IsNullOrEmpty(publicKeyStr))
        {
            return null;
        }

        byte[] signature;
        byte[] publicKey;
        try
        {
            signature = Convert.FromBase64String(signatureStr);
            publicKey = Convert.FromBase64String(publicKeyStr);
        }
        catch (FormatException ex)
        {
            Logger.F(TAG, ex);
            return null;
        }

        string? purchaseInfoJson = tokenObject.PurchaseInfoJson;
        if (string.IsNullOrEmpty(purchaseInfoJson))
        {
            return null;
        }

        PurchaseInfoJsonModel? purchaseInfoJsonModel;
        try
        {
            purchaseInfoJsonModel = JsonSerializer.Deserialize<PurchaseInfoJsonModel>(purchaseInfoJson);
        }
        catch (JsonException ex)
        {
            Logger.F(TAG, ex);
            return null;
        }

        if (purchaseInfoJsonModel is null)
        {
            return null;
        }

        var purchaseInfo = PurchaseInfo.FromJsonModel(purchaseInfoJsonModel);
        if (purchaseInfo is null)
        {
            return null;
        }

        string deviceId = EnvironmentProvider.Instance.GetActualDeviceId();
        string payloadStr = $"{deviceId}|{purchaseInfoJson}";
        byte[] payload = Encoding.UTF8.GetBytes(payloadStr);
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
            if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                return null;
            }
        }
        catch (CryptographicException ex)
        {
            Logger.F(TAG, ex);
            return null;
        }

        return purchaseInfo;
    }

    public class OperationResult
    {
        public required bool Successful { get; init; }
        public required string ErrorMessage { get; init; }

        public static OperationResult From(bool successful, Exception? exception)
        {
            string errorMessage = string.Empty;
            if (!successful)
            {
                string description = string.Empty;
                string code = string.Empty;

                if (exception is not null)
                {
                    description = exception.Message;
                    if (exception is COMException comException)
                    {
                        code = $"0x{comException.ErrorCode:X8}";
                    }
                }

                if (string.IsNullOrEmpty(description))
                {
                    description = "Unknown error";
                }

                if (string.IsNullOrEmpty(code))
                {
                    errorMessage = description;
                }
                else
                {
                    errorMessage = $"{description} ({code})";
                }
            }

            return new()
            {
                Successful = successful,
                ErrorMessage = errorMessage,
            };
        }
    }

    private class PurchaseInfo
    {
        public required string Name { get; init; }
        public required DateTime PurchaseDate { get; init; }

        public PurchaseInfoJsonModel ToJsonModel()
        {
            return new PurchaseInfoJsonModel
            {
                Version = 1,
                Name = Name,
                PurchaseDate = PurchaseDate,
            };
        }

        public static PurchaseInfo? FromJsonModel(PurchaseInfoJsonModel model)
        {
            if (model.Name is null || model.PurchaseDate is null || model.Version is null)
            {
                return null;
            }

            if (model.Version != 1)
            {
                return null;
            }

            return new PurchaseInfo
            {
                Name = model.Name,
                PurchaseDate = model.PurchaseDate.Value,
            };
        }
    }

    private class PurchaseInfoJsonModel
    {
        [JsonPropertyName("Version")]
        public required int? Version { get; init; }

        [JsonPropertyName("Name")]
        public required string? Name { get; init; }

        [JsonPropertyName("PurchaseDate")]
        public required DateTime? PurchaseDate { get; init; }
    }

    private class PurchaseTokenJsonModel
    {
        [JsonPropertyName("Signature")]
        public required string? Signature { get; init; }

        [JsonPropertyName("PublicKey")]
        public required string? PublicKey { get; init; }

        [JsonPropertyName("PurchaseInfoJson")]
        public required string? PurchaseInfoJson { get; init; }
    }
}
