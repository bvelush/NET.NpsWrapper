using OpenCymd.Nps.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Omni2FA.Net.Utils {
    public static class Radius {
        public static string AttributeLookup(IList<RadiusAttribute> attributesList, RadiusAttributeType attributeType) {
            var a = attributesList.FirstOrDefault(x => x.AttributeId.Equals((int)attributeType));
            if (a == null)
                return string.Empty;
            var ret_val = (a.Value is byte[] val) ? Encoding.Default.GetString(val) : a.Value.ToString();
            return Str.sanitize(ret_val);
        }
        /* Get all attributes*/
        public static List<string> AttributesToList(IList<RadiusAttribute> attributesList) {
            var r = new List<string>();
            foreach (var attrib in attributesList) {
                string attribName = (Enum.IsDefined(typeof(RadiusAttributeType), attrib.AttributeId)) ?
                    ((RadiusAttributeType)attrib.AttributeId).ToString()
                    : attrib.AttributeId.ToString();
                string attribValue = attrib.Value is byte[] val ? Encoding.Default.GetString(val) : attrib.Value.ToString();
                r.Add($"{attribName}: {attribValue}");
            }
            return r;
        }
    }
}
