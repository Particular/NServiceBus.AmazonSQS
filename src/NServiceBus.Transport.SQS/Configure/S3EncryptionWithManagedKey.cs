namespace NServiceBus
{
    using Amazon.S3;
    using Amazon.S3.Model;

    /// <summary>
    /// S3 managed key encryption.
    /// </summary>
    public sealed class S3EncryptionWithManagedKey : S3EncryptionMethod
    {
        /// <summary>
        /// Creates new S3 encryption settings.
        /// </summary>
        /// <param name="method">Encryption method.</param>
        /// <param name="keyId">Encryption key id.</param>
        public S3EncryptionWithManagedKey(ServerSideEncryptionMethod method, string keyId = null)
        {
            Method = method;
            KeyId = keyId;
        }

        /// <summary>
        /// Encryption method.
        /// </summary>
        public ServerSideEncryptionMethod Method { get; }

        /// <summary>
        /// Encryption key ID.
        /// </summary>
        public string KeyId { get; }

        /// <summary>
        /// Modifies the get request to retrieve the message body from S3.
        /// </summary>
        protected internal override void ModifyGetRequest(GetObjectRequest get)
        {
        }

        /// <summary>
        /// Modifies the put request to upload the message body to S3.
        /// </summary>
        protected internal override void ModifyPutRequest(PutObjectRequest put)
        {
            put.ServerSideEncryptionMethod = Method;

            if (!string.IsNullOrEmpty(KeyId))
            {
                put.ServerSideEncryptionKeyManagementServiceKeyId = KeyId;
            }
        }
    }
}