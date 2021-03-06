﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using TTController.Common;
using TTController.Service.Controller.Definition;
using TTController.Service.Hardware;

namespace TTController.Service.Controller.Proxy
{
    public class DpsgControllerProxy : AbstractControllerProxy
    {
        private readonly IReadOnlyDictionary<string, byte> _availableEffects;

        public DpsgControllerProxy(IHidDeviceProxy device, IControllerDefinition definition)
            : base(device, definition)
        {
            _availableEffects = new Dictionary<string, byte>()
            {
                ["ByLed"] = 0x18,
                ["Full"] = 0x19
            };
        }

        public override IEnumerable<PortIdentifier> Ports
        {
            get
            {
                yield return new PortIdentifier(Device.VendorId, Device.ProductId, 0);
            }
        }

        public override IEnumerable<string> EffectTypes => _availableEffects.Keys;

        public override bool SetRgb(byte port, byte mode, IEnumerable<LedColor> colors)
        {
            if (port != 0)
                return false;

            var bytes = new List<byte> { 0x30, 0x42, mode };
            foreach (var color in colors)
            {
                bytes.Add(color.G);
                bytes.Add(color.R);
                bytes.Add(color.B);
            }

            var result = Device.WriteReadBytes(bytes);
            return result[3] == 0xfc;
        }

        public override bool SetSpeed(byte port, byte speed)
        {
            if (port != 0)
                return false;

            var result = Device.WriteReadBytes(0x30, 0x41, 0x04, speed);
            return result[3] == 0xfc;
        }

        public override PortData GetPortData(byte port)
        {
            if (port != 0)
                return null;
            
            // 0x31, 0x33 // VIN
            // 0x31, 0x34 // VVOut12
            // 0x31, 0x35 // VVout5
            // 0x31, 0x36 // VVOut33
            // 0x31, 0x37 // VIOut12
            // 0x31, 0x38 // VIOut5
            // 0x31, 0x39 // VIOut33
            // 0x31, 0x3a // Temp
            // 0x31, 0x3b // FanSpeed
            // WATTS = VVOut33 * VIOut33
            // EFF = (int)((VVOut12 * VIOut12 + VVOut5 * VIOut5 + VVOut33 * VIOut33) / 10.0)

            byte[] GetData(byte b) => Device.WriteReadBytes(0x31, b).Take(2).ToArray();
            string GetDataAsString(byte b) => $"{string.Join("", GetData(b).Select(x => x.ToString("X2")))}";

            var data = new PortData()
            {
                ["VIN"] = GetDataAsString(0x33),
                ["VVOut12"] = GetDataAsString(0x34),
                ["VVout5"] = GetDataAsString(0x35),
                ["VVOut33"] = GetDataAsString(0x36),
                ["VIOut12"] = GetDataAsString(0x37),
                ["VIOut5"] = GetDataAsString(0x38),
                ["VIOut33"] = GetDataAsString(0x39), 
                ["Temp"] = GetDataAsString(0x3a),
                ["FanSpeed"] = GetDataAsString(0x3b)
            };

            return data;
        }

        public override byte? GetEffectByte(string effectType)
        {
            if (effectType == null)
                return null;
            return _availableEffects.TryGetValue(effectType, out var value) ? value : (byte?)null;
        }

        public override void SaveProfile()
        {
            return;
        }

        public override bool Init()
        {
            var result = Device.WriteReadBytes(0xfe, 0x31);
            if (result == null || result.Length == 0)
                return false;

            try
            {
                var model = Encoding.ASCII.GetString(result, 0, result.Length);
                return !string.IsNullOrEmpty(model);
            }
            catch { return false; }
        }

        public override bool IsValidPort(PortIdentifier port) =>
            port.ControllerProductId == Device.ProductId &&
           port.ControllerVendorId == Device.VendorId &&
           port.Id == 0;
    }
}
