// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.Constants;
using ComicReader.SDK.Common.ServiceManagement;
using ComicReader.SDK.Common.Utils;
using ComicReader.SDK.Database.KV;

namespace ComicReader.SDK.Common.DebugTools;

public static class DebugCommand
{
    private const string TAG = nameof(DebugCommand);
    private const int SIGNATURE_LENGTH = 256;
    private const string PUBLIC_KEY_PEM = @"-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEAot89oOONQcVUgUft6YLU15yntMd+Ve1pM7kU5Lr61T8hFnfFxL7x
tLmodYK+o+FTPKtcoWglxA1fp9cHjRaRI7SUYqPyixXxepGeMMf0NUndduScthTk
ZNuH9P/la/gFTq+yb9bWhjH1HNLAMD/XaUjF+6eVlE0CVcslVEde6foHlYqjMQlp
Mv5FKy9jFQCHhFcfvXaP4yd8bCE3pL2x43qEbGQw9iOufGCgFplckblXy9OFmbgB
xpRSqAgubJDMUR3a8NNWEmKaKfKTbY85OV0Qe1mYo4DWPJRYywjglHmUY+IHxoPV
TKf0Mms0jR50tiagNV2oHZlD9pKTTBnzsQIDAQAB
-----END RSA PUBLIC KEY-----";

    private static bool? _unlockedDeveloperMode = null;
    internal static bool UnlockedDeveloperMode
    {
        get
        {
            if (!_unlockedDeveloperMode.HasValue)
            {
                string? token = KVStore.Sdk.GetCollection(DatabaseEntry.KV_LIB_MAIN).GetValue<string>(DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE_TOKEN);
                bool tokenValid = token != null && ParseCommand(token) != null;
                _unlockedDeveloperMode = tokenValid;
            }

            return _unlockedDeveloperMode.Value;
        }
    }

    public static bool TryExecute(string command)
    {
        string? parsedCommand = ParseCommand(command);
        if (parsedCommand == null)
        {
            return false;
        }

        // Enable developer mode
        _unlockedDeveloperMode = true;
        KVStore.Sdk.GetCollection(DatabaseEntry.KV_LIB_MAIN).Set(DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE_TOKEN, command);
        DebugUtils.DeveloperMode = true;

        return ProcessCommand(parsedCommand);
    }

    private static string? ParseCommand(string signedCommand)
    {
        signedCommand = signedCommand.Trim();
        byte[]? signatureAndCommandBytes = DecodeWithBase64(signedCommand);
        if (signatureAndCommandBytes == null || signatureAndCommandBytes.Length < SIGNATURE_LENGTH)
        {
            return null;
        }

        byte[] signatureBytes = new byte[SIGNATURE_LENGTH];
        Array.Copy(signatureAndCommandBytes, 0, signatureBytes, 0, SIGNATURE_LENGTH);

        byte[] commandBytes = new byte[signatureAndCommandBytes.Length - SIGNATURE_LENGTH];
        Array.Copy(signatureAndCommandBytes, SIGNATURE_LENGTH, commandBytes, 0, signatureAndCommandBytes.Length - SIGNATURE_LENGTH);
        string command = System.Text.Encoding.UTF8.GetString(commandBytes);

        string developerId = EnvironmentProvider.Instance.GetDeveloperId();
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes($"{developerId}+{command}");
        if (!VerifySignature(payloadBytes, signatureBytes, PUBLIC_KEY_PEM))
        {
            return null;
        }

        return command;
    }

    private static bool VerifySignature(byte[] payloadBytes, byte[] signatureBytes, string publicKeyPem)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            byte[] hash = SHA256.HashData(payloadBytes);
            return rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "Error verifying signature.", e);
            return false;
        }
    }

    private static byte[]? DecodeWithBase64(string input)
    {
        try
        {
            return Convert.FromBase64String(input);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool ProcessCommand(string command)
    {
        IDebugService? debugService = ServiceManager.GetServiceNullable<IDebugService>();
        return debugService != null && debugService.HandleDebugCommand(command);
    }
}
