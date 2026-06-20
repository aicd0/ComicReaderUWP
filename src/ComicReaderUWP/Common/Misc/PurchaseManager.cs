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

using ComicReaderUWP.Common.Constants;
using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.DebugTools;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Data.Database;
using ComicReaderUWP.Views.AppWindows.Main;

using Windows.Services.Store;

namespace ComicReaderUWP.Common.Misc;

internal static class PurchaseManager
{
    private const string TAG = nameof(PurchaseManager);
    private const string KEY_DONOR_TOKEN = "DonorToken";
    private const string ITEM_NAME_DONOR = "Donor";

    private static string DonorStoreId => DebugModel.DonorStoreId ?? SecretImpl.StoreIdDonor;

    private static bool? _isDonor;
    public static bool IsDonor
    {
        get
        {
            if (!_isDonor.HasValue)
            {
                string? token = AppDB.AppKV.GetCollection(KVNames.KV_LIB_PURCHASES).GetValue<string>(KEY_DONOR_TOKEN);
                PurchaseInfo? info = VerifyPurchaseToken(token, SecretImpl.Salt1);
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
                string token = CreatePurchaseToken(info, SecretImpl.Salt1);
                AppDB.AppKV.GetCollection(KVNames.KV_LIB_PURCHASES).Set(KEY_DONOR_TOKEN, token);
            }
            else
            {
                AppDB.AppKV.GetCollection(KVNames.KV_LIB_PURCHASES).Set<string>(KEY_DONOR_TOKEN, null);
            }
        }
    }

    public static async Task<OperationResult> UpdatePurchaseStatus(int windowId)
    {
        StoreContext? context = GetStoreContext(windowId);
        if (context is null)
        {
            return OperationResult.From(false, new InvalidOperationException("Failed to get StoreContext."));
        }

        StoreProductQueryResult result = await context.GetUserCollectionAsync(["Durable"]);
        if (result.ExtendedError is not null)
        {
            return OperationResult.From(false, result.ExtendedError);
        }

        foreach (KeyValuePair<string, StoreProduct> pair in result.Products)
        {
            string storeId = pair.Key;
            if (storeId == DonorStoreId)
            {
                IsDonor = true;
            }
        }

        return OperationResult.From(true, null);
    }

    public static async Task<OperationResult> PurchaseDonor(int windowId)
    {
        StoreContext? context = GetStoreContext(windowId);
        if (context is null)
        {
            return OperationResult.From(false, new InvalidOperationException("Failed to get StoreContext."));
        }

        StorePurchaseResult result = await context.RequestPurchaseAsync(DonorStoreId);
        bool successful = false;
        switch (result.Status)
        {
            case StorePurchaseStatus.Succeeded:
            case StorePurchaseStatus.AlreadyPurchased:
                IsDonor = true;
                successful = true;
                break;
            case StorePurchaseStatus.NotPurchased:
                IsDonor = false;
                break;
            case StorePurchaseStatus.NetworkError:
            case StorePurchaseStatus.ServerError:
                break;
            default:
                break;
        }
        return OperationResult.From(successful, result.ExtendedError);
    }

    //
    // Test API
    //

    public static void MockDonorStatus(bool isDonor)
    {
        IsDonor = isDonor;
    }

    //
    // Helpers
    //

    private static StoreContext? GetStoreContext(int preferredWindowId)
    {
        var context = StoreContext.GetDefault();
        MainWindow? window = App.Instance.WindowManager.GetWindow(preferredWindowId) ??
            App.Instance.WindowManager.GetActiveWindow() ??
            App.Instance.WindowManager.GetAnyWindow();
        if (window is null)
        {
            return null;
        }

        nint hWnd = window.WindowHandle;
        WinRT.Interop.InitializeWithWindow.Initialize(context, hWnd);
        return context;
    }

    private static string CreatePurchaseToken(PurchaseInfo info, string salt)
    {
        string purchaseInfoJson = JsonSerializer.Serialize(info.ToJsonModel());
        string deviceId = EnvironmentProvider.Instance.GetActualDeviceId();
        string payloadStr = $"{salt}|{deviceId}|{purchaseInfoJson}";
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

    private static PurchaseInfo? VerifyPurchaseToken(string? token, string salt)
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
        string payloadStr = $"{salt}|{deviceId}|{purchaseInfoJson}";
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
