// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using ComicReaderUWP.Core.Common.AppEnvironment;
using ComicReaderUWP.Core.Common.Constants;
using ComicReaderUWP.Core.Common.ServiceManagement;
using ComicReaderUWP.Core.Common.ServiceManagement.Services;
using ComicReaderUWP.Core.Common.Utils;
using ComicReaderUWP.Core.Database.Misc;

namespace ComicReaderUWP.Core.Common.DebugTools;

public static class DebugCommand
{
    private const string TAG = nameof(DebugCommand);
    private const string KEY_DEVELOPER_MODE_TOKEN = "DeveloperModeToken";
    private const int SIGNATURE_LENGTH = 256;

    private static bool? _unlockedDeveloperMode = null;
    internal static bool UnlockedDeveloperMode
    {
        get
        {
            if (!_unlockedDeveloperMode.HasValue)
            {
                string? token = CoreDB.CoreRegistry.CreateKey(RegistryNames.DEBUG_SETTINGS).GetValue<string>(KEY_DEVELOPER_MODE_TOKEN);
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
        CoreDB.CoreRegistry.CreateKey(RegistryNames.DEBUG_SETTINGS).Set(KEY_DEVELOPER_MODE_TOKEN, command);
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

        string developerId = EnvironmentProvider.Instance.GetDeviceId();
        byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes($"{developerId}+{command}");

        string? publicKeyPem = ServiceManager.GetServiceNullable<IDebugService>()?.DebugCommandPublicKeyPem;
        if (string.IsNullOrEmpty(publicKeyPem))
        {
            return null;
        }

        if (!VerifySignature(payloadBytes, signatureBytes, publicKeyPem))
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
        catch (Exception ex)
        {
            Logger.F(TAG, "Error verifying signature", ex);
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
