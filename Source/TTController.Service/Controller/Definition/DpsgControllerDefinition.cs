﻿using System;
using System.Collections.Generic;
using TTController.Service.Controller.Proxy;

namespace TTController.Service.Controller.Definition
{
    public class DpsgControllerDefinition : IControllerDefinition
    {
        public string Name => "Dpsg Controller";
        public int VendorId => 0x264a;
        public IEnumerable<int> ProductIds { get { yield return 0x2329; } }
        public int PortCount => 0;
        public Type ControllerProxyType => typeof(DpsgControllerProxy);
    }
}
