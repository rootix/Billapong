﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Billapong.Contract.Data;
using Billapong.Contract.Service;
using Billapong.Core.Client;

namespace Billapong.GameConsole
{
    public class ServiceClient : RichClientBase<IConsoleService>, IConsoleService
    {
        public ServiceClient(Binding binding, EndpointAddress endpointAddress) : base(binding, endpointAddress)
        {
            // todo: refactore and move to specific project
        }

        public IEnumerable<Map> GetMaps()
        {
            return base.Execute(() => base.Proxy.GetMaps());
        }
    }
}