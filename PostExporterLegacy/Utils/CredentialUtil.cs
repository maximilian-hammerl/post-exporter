using System;
using Meziantou.Framework.Win32;

namespace PostExporter.Utils;

public static class CredentialUtil
{
    public static void UpdateCredential(string baseUrl, string username, string? password)
    {
        CredentialManager.WriteCredential(
            Util.GetAppName(),
            comment: baseUrl,
            userName: username,
            secret: password ?? "",
            persistence: CredentialPersistence.LocalMachine);
    }

    public static (string BaseUrl, string Username, string? Password)? ReadCredential()
    {
        var credential = CredentialManager.ReadCredential(Util.GetAppName());

        if (credential is null)
        {
            return null;
        }

        return (
            BaseUrl: credential.Comment ?? throw new InvalidOperationException("Credential without base url!"),
            Username: credential.UserName ?? throw new InvalidOperationException("Credential without username!"),
            Password: string.IsNullOrWhiteSpace(credential.Password) ? null : credential.Password
        );
    }

    public static void DeleteCredential()
    {
        if (HasCredential())
        {
            CredentialManager.DeleteCredential(Util.GetAppName());
        }
    }

    private static bool HasCredential()
    {
        return CredentialManager.ReadCredential(Util.GetAppName()) is not null;
    }
}