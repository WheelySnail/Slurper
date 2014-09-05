﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CandidateParsingAgilityPack
{
    using System.Web;

    using CsQuery.Output;

    using Newtonsoft.Json;
        public class HtmlEncodeStringPropertiesConverter : JsonConverter
        {
            private HtmlEncoderBasic HtmlEncoderBasic { get; set; }
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(string);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                //writer.WriteValue(HtmlEncode(value.ToString()));
                throw new NotImplementedException();
            }
        }
   
}
