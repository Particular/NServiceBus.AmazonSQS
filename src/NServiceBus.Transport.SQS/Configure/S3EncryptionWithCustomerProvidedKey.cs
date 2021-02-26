namespace NServiceBus
{
    using Amazon.S3;
    using Amazon.S3.Model;

    /// <summary>
    /// S3 customer-provided key encryption.
    /// </summary>
    public sealed class S3EncryptionWithCustomerProvidedKey : S3EncryptionMethod
    {
        /// <summary>
        /// Creates new S3 encryption settings.
        /// </summary>
        /// <param name="method">Encryption method.</param>
        /// <param name="key">Encryption key.</param>
        /// <param name="keyMd5">Encryption key MD5 checksum.</param>
        public S3EncryptionWithCustomerProvidedKey(ServerSideEncryptionCustomerMethod method, string key, string keyMd5 = null)
        {
            Method = method;
            Key = key;
            KeyMD5 = keyMd5;
        }

        /// <summary>
        /// Encryption method.
        /// </summary>
        public ServerSideEncryptionCustomerMethod Method { get; }

        /// <summary>
        /// Encryption key.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Encryption key MD5 checksum.
        /// </summary>
        public string KeyMD5 { get; }

        /// <summary>
        /// Modifies the get request to retrieve the message body from S3.
        /// </summary>
        protected internal override void ModifyGetRequest(GetObjectRequest get)
        {
            get.ServerSideEncryptionCustomerMethod = Method;
            get.ServerSideEncryptionCustomerProvidedKey = Key;

            if (!string.IsNullOrEmpty(KeyMD5))
            {
                get.ServerSideEncryptionCustomerProvidedKeyMD5 = KeyMD5;
            }
        }

        /// <summary>
        /// Modifies the put request to upload the message body to S3.
        /// </summary>
        protected internal override void ModifyPutRequest(PutObjectRequest put)
        {
            put.ServerSideEncryptionCustomerMethod = Method;
            put.ServerSideEncryptionCustomerProvidedKey = Key;

            if (!string.IsNullOrEmpty(KeyMD5))
            {
                put.ServerSideEncryptionCustomerProvidedKeyMD5 = KeyMD5;
            }
        }
    }
}