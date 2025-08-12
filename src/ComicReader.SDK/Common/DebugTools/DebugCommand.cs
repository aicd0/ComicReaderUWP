// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.Constants;
using ComicReader.SDK.Common.KVStorage;
using ComicReader.SDK.Common.ServiceManagement;

namespace ComicReader.SDK.Common.DebugTools;

public static class DebugCommand
{
    private const string TAG = nameof(DebugCommand);
    private const int SIGNATURE_LENGTH = 256;
    private const string PUBLIC_KEY_PEM = @"-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEAmyJ4ckZZJaRPsTrHz2VNB+rV7sFb2c9L2aaxg72D71vR44KPFVJO
mhzCPtYNIsghteqUVGUevlrWAEaomnROWSuygLwIfsXV+0fC524Jxgls50lYxsFV
DpoNPybbClgakzEX8v0XOxEMbykzE7y+sNk4BFMSqMrN0vn24EH/9W0UZZQzsIfR
Mxo8Z+rYxNV8Orkf4CSIRL7h9UFFr6EkaApGpgqVl3QvQvNrSo5OCqELsZ/4/DF3
xp8vQPBayknp/N1WAT768SYpXAT/nta/ddJnCkbMsCd/C1AZhDDwsjk4+Bsmj3DK
5RycCc4/1JVY+rervfzfCzXLTOyPdmvE6QIDAQAB
-----END RSA PUBLIC KEY-----";

    private static bool? _unlockedDeveloperMode = null;
    internal static bool UnlockedDeveloperMode
    {
        get
        {
            if (!_unlockedDeveloperMode.HasValue)
            {
                string? token = KVDatabase.Sdk.GetString(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE_TOKEN);
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
        KVDatabase.Sdk.SetString(DatabaseEntry.KV_LIB_MAIN, DatabaseEntry.KV_KEY_MAIN_DEVELOPER_MODE_TOKEN, command);
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

        string versionName = EnvironmentProvider.GetVersionName();
        string deviceId = EnvironmentProvider.Instance.GetDeviceId();

        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes($"{versionName}+{deviceId}+{command}");
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
