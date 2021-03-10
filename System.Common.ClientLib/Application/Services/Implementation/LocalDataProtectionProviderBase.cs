﻿using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static System.Application.Services.ILocalDataProtectionProvider;

namespace System.Application.Services.Implementation
{
    /// <inheritdoc cref="ILocalDataProtectionProvider"/>
    public abstract class LocalDataProtectionProviderBase : ILocalDataProtectionProvider
    {
        protected readonly Aes aes;
        protected readonly LocalDataProtectionType defaultELocalDataProtectionType;
        protected readonly IProtectedData protectedData;
        protected readonly IDataProtectionProvider dataProtectionProvider;

        public LocalDataProtectionProviderBase(
            IProtectedData protectedData,
            IDataProtectionProvider dataProtectionProvider)
        {
            this.protectedData = protectedData;
            this.dataProtectionProvider = dataProtectionProvider;
            switch (DI.Platform)
            {
                case Platform.Windows:
                    if (Environment.OSVersion.Version.Major >= 10)
                    {
                        defaultELocalDataProtectionType = LocalDataProtectionType.Win10WithAesOFB;
                    }
                    else
                    {
                        defaultELocalDataProtectionType = LocalDataProtectionType.ProtectedDataWithAesOFB;
                    }
                    break;
                case Platform.Linux:
                    defaultELocalDataProtectionType = LocalDataProtectionType.AesOFB;
                    break;
                default:
                    defaultELocalDataProtectionType = LocalDataProtectionType.None;
                    break;
            }
            (byte[] key, byte[] iv) = MachineSecretKey;
            aes = AESUtils.Create(key, iv, CipherMode.CFB, PaddingMode.PKCS7);
        }

        protected enum LocalDataProtectionType
        {
            None,

            AesOFB,

            ProtectedDataWithAesOFB,

            Win10WithAesOFB,
        }

        protected abstract (byte[] key, byte[] iv) MachineSecretKey { get; }

        byte[] Concat(byte[] value)
        {
            var r = BitConverter.GetBytes((int)defaultELocalDataProtectionType).Concat(value).ToArray();
            return r;
        }

        byte[] E___(byte[] value)
        {
            var r = AESUtils.Encrypt(aes, value);
            return r;
        }

        public async ValueTask<byte[]?> EB(byte[]? value)
        {
            if (value == null) return value;
            if (value.Length == 0) return value;
            switch (defaultELocalDataProtectionType)
            {
                case LocalDataProtectionType.None:
                    return value;
                case LocalDataProtectionType.AesOFB:
                    var value_1 = E___(value);
                    var value_1_r = Concat(value_1);
                    return value_1_r;
                case LocalDataProtectionType.ProtectedDataWithAesOFB:
                    var value_2 = E___(value);
                    var value_2_pd = protectedData.Protect(value_2);
                    var value_2_r = Concat(value_2_pd);
                    return value_2_r;
                case LocalDataProtectionType.Win10WithAesOFB:
                    var value_3 = E___(value);
                    var value_3_dpp = await dataProtectionProvider.ProtectAsync(value_3);
                    var value_3_r = Concat(value_3_dpp);
                    return value_3_r;
                default:
                    throw new ArgumentOutOfRangeException(nameof(defaultELocalDataProtectionType), defaultELocalDataProtectionType, null);
            }
        }

        byte[]? D___(byte[] value)
        {
            using var transform = aes.CreateDecryptor();
            var data_r = transform.TransformFinalBlock(value, sizeof(int), value.Length - sizeof(int));
            return data_r;
        }

        static byte[] UnConcat(byte[] value) => value.Skip(sizeof(int)).ToArray();

        public async ValueTask<byte[]?> DB(byte[]? value)
        {
            if (value == null) return value;
            if (value.Length == 0) return value;
            if (value.Length <= sizeof(int)) return null;

            var d_type = (LocalDataProtectionType)BitConverter.ToInt32(value, 0);
            try
            {
                switch (d_type)
                {
                    case LocalDataProtectionType.None:
                        return value;
                    case LocalDataProtectionType.AesOFB:
                        return D___(value);
                    case LocalDataProtectionType.ProtectedDataWithAesOFB:
                        var value_2 = UnConcat(value);
                        var value_2_pd = protectedData.Unprotect(value_2);
                        var value_2_r = AESUtils.Decrypt(aes, value_2_pd);
                        return value_2_r;
                    case LocalDataProtectionType.Win10WithAesOFB:
                        var value_3 = UnConcat(value);
                        var value_3_dpp = await dataProtectionProvider.UnprotectAsync(value_3);
                        var value_3_r = AESUtils.Decrypt(aes, value_3_dpp);
                        return value_3_r;
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public sealed class EmptyProtectedData : IProtectedData
        {
            public byte[] Protect(byte[] userData) => userData;

            public byte[] Unprotect(byte[] encryptedData) => encryptedData;
        }

        public sealed class EmptyDataProtectionProvider : IDataProtectionProvider
        {
            public Task<byte[]> ProtectAsync(byte[] data) => Task.FromResult(data);

            public Task<byte[]> UnprotectAsync(byte[] data) => Task.FromResult(data);
        }
    }
}