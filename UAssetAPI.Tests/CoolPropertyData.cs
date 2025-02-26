﻿using System;
using System.IO;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;

namespace UAssetAPI.PropertyTypes.Objects
{
    /// <summary>
    /// Describes something very cool!
    /// </summary>
    public class CoolPropertyData : PropertyData<int>
    {
        public CoolPropertyData(FName name) : base(name)
        {

        }

        public CoolPropertyData()
        {

        }

        private static readonly FString CurrentPropertyType = new FString("CoolProperty");
        public override FString PropertyType { get { return CurrentPropertyType; } }

        public override void Read(AssetBinaryReader reader, bool includeHeader, long leng1, long leng2 = 0, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
        {
            if (includeHeader)
            {
                this.ReadEndPropertyTag(reader);
            }
            Value = (int)reader.ReadByte();
        }

        public override int Write(AssetBinaryWriter writer, bool includeHeader, PropertySerializationContext serializationContext = PropertySerializationContext.Normal)
        {
            if (includeHeader)
            {
                this.WriteEndPropertyTag(writer);
            }

            writer.Write((byte)Value);
            return sizeof(byte);
        }

        public override string ToString()
        {
            return Convert.ToString(Value);
        }

        public override void FromString(string[] d, UAsset asset)
        {
            if (int.TryParse(d[0], out int x)) Value = x;
        }
    }
}