using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using PromptMasterv6.Core.Helpers;

namespace PromptMasterv6.Core.Converters
{
    public class JsonEncryptedStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? encryptedValue = reader.GetString();
            if (string.IsNullOrEmpty(encryptedValue))
            {
                return encryptedValue;
            }

            return EncryptionHelper.Unprotect(encryptedValue);
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (string.IsNullOrEmpty(value))
            {
                writer.WriteStringValue(value);
                return;
            }

            string encryptedValue = EncryptionHelper.Protect(value);
            writer.WriteStringValue(encryptedValue);
        }
    }
}
