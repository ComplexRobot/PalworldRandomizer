﻿using Newtonsoft.Json;
using System;
using UAssetAPI.JSON;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;

namespace UAssetAPI.PropertyTypes.Objects
{
    /// <summary>
    /// Describes an IEEE 64-bit floating point variable (<see cref="double"/>).
    /// </summary>
    public class DoublePropertyData : PropertyData
    {
        /// <summary>
        /// The double that this property represents.
        /// </summary>
        [JsonProperty]
        [JsonConverter(typeof(FSignedZeroJsonConverter))]
        public double Value;

        public DoublePropertyData(FName name) : base(name)
        {

        }

        public DoublePropertyData()
        {

        }

        private static readonly FString CurrentPropertyType = new FString("DoubleProperty");
        public override FString PropertyType { get { return CurrentPropertyType; } }
        public override object DefaultValue { get { return (double)0; } }

        public override void Read(AssetBinaryReader reader, bool includeHeader, long leng1, long leng2 = 0, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
        {
            if (includeHeader)
            {
                this.ReadEndPropertyTag(reader);
            }

            Value = reader.ReadDouble();
        }

        public override int Write(AssetBinaryWriter writer, bool includeHeader, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
        {
            if (includeHeader)
            {
                this.WriteEndPropertyTag(writer);
            }

            writer.Write(Value);
            return sizeof(double);
        }

        public override string ToString()
        {
            return Convert.ToString(Value);
        }

        public override void FromString(string[] d, UAsset asset)
        {
            Value = 0;
            if (double.TryParse(d[0], out double res)) Value = res;
        }
    }
}