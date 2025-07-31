// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Security.Cryptography;

using ComicReader.Helpers.Navigation;
using ComicReader.SDK.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Views.DevTools;

internal static class InternalCommand
{
    private const string TAG = nameof(InternalCommand);
    private const int SIGNATURE_LENGTH = 256;
    private const string PUBLIC_KEY_PEM = @"-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEAmyJ4ckZZJaRPsTrHz2VNB+rV7sFb2c9L2aaxg72D71vR44KPFVJO
mhzCPtYNIsghteqUVGUevlrWAEaomnROWSuygLwIfsXV+0fC524Jxgls50lYxsFV
DpoNPybbClgakzEX8v0XOxEMbykzE7y+sNk4BFMSqMrN0vn24EH/9W0UZZQzsIfR
Mxo8Z+rYxNV8Orkf4CSIRL7h9UFFr6EkaApGpgqVl3QvQvNrSo5OCqELsZ/4/DF3
xp8vQPBayknp/N1WAT768SYpXAT/nta/ddJnCkbMsCd/C1AZhDDwsjk4+Bsmj3DK
5RycCc4/1JVY+rervfzfCzXLTOyPdmvE6QIDAQAB
-----END RSA PUBLIC KEY-----";

    public static bool Parse(string encryptedCommand)
    {
        encryptedCommand = encryptedCommand.Trim();
        byte[]? signatureAndPayloadBytes = DecodeWithBase64(encryptedCommand);
        if (signatureAndPayloadBytes == null || signatureAndPayloadBytes.Length < SIGNATURE_LENGTH)
        {
            return false;
        }

        byte[] signatureBytes = new byte[SIGNATURE_LENGTH];
        Array.Copy(signatureAndPayloadBytes, 0, signatureBytes, 0, SIGNATURE_LENGTH);

        string deviceId = EnvironmentProvider.Instance.GetDeviceId();
        byte[] deviceIdBytes = System.Text.Encoding.UTF8.GetBytes(deviceId);
        byte[] payloadBytes = new byte[signatureAndPayloadBytes.Length - SIGNATURE_LENGTH + deviceIdBytes.Length];
        Array.Copy(deviceIdBytes, 0, payloadBytes, 0, deviceIdBytes.Length);
        Array.Copy(signatureAndPayloadBytes, SIGNATURE_LENGTH, payloadBytes, deviceIdBytes.Length, signatureAndPayloadBytes.Length - SIGNATURE_LENGTH);
        if (!VerifySignature(payloadBytes, signatureBytes, PUBLIC_KEY_PEM))
        {
            return false;
        }

        byte[] commandBytes = new byte[signatureAndPayloadBytes.Length - SIGNATURE_LENGTH];
        Array.Copy(signatureAndPayloadBytes, SIGNATURE_LENGTH, commandBytes, 0, signatureAndPayloadBytes.Length - SIGNATURE_LENGTH);
        string command = System.Text.Encoding.UTF8.GetString(commandBytes);
        return ProcessCommand(command);
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
        if (command == "dev_tools")
        {
            var newWindow = new MainWindow(RouterConstants.SCHEME_APP + RouterConstants.HOST_DEV_TOOLS);
            newWindow.Activate();
            return true;
        }

        return false;
    }
}
