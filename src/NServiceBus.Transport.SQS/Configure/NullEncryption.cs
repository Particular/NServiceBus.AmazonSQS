namespace NServiceBus
{
    using Amazon.S3.Model;

    class NullEncryption : S3EncryptionMethod
    {
        public static readonly NullEncryption Instance = new NullEncryption();

        protected internal override void ModifyGetRequest(GetObjectRequest get)
        {
        }

        protected internal override void ModifyPutRequest(PutObjectRequest put)
        {
        }
    }
}