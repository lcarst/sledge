﻿using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.Common.Transport;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Primitives.MapObjectData
{
    public class EntityData : IMapObjectData
    {
        public string Name { get; set; }
        public int Flags { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public EntityData()
        {
            Properties = new Dictionary<string, string>();
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Properties", Properties);
        }

        public Coordinate GetCoordinate(string key)
        {
            if (!Properties.ContainsKey(key)) return null;
            var spl = (Properties[key] ?? "").Split(' ');
            decimal x, y, z;
            if (decimal.TryParse(spl[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                && decimal.TryParse(spl[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && decimal.TryParse(spl[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                return new Coordinate(x, y, z);
            }
            return null;
        }

        public void Set(string key, string value)
        {
            Properties[key] = value;
        }

        public void Unset(string key)
        {
            if (Properties.ContainsKey(key)) Properties.Remove(key);
        }

        public IMapElement Clone()
        {
            var ed = new EntityData();
            ed.Name = Name;
            ed.Flags = Flags;
            ed.Properties = new Dictionary<string, string>(Properties);
            return ed;
        }

        public IMapElement Copy(UniqueNumberGenerator numberGenerator)
        {
            return Clone();
        }

        public SerialisedObject ToSerialisedObject()
        {
            var so = new SerialisedObject("EntityData");
            foreach (var p in Properties)
            {
                so.Set(p.Key, p.Value);
            }
            so.Set("Name", Name);
            so.Set("Flags", Flags);
            return so;
        }
    }
}