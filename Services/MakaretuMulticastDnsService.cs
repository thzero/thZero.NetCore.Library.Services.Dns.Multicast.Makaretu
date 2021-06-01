/* ------------------------------------------------------------------------- *
thZero.NetCore.Library.Services.Dns.Multicast.Makaretu
Copyright (C) 2021-2021 thZero.com

<development [at] thzero [dot] com>

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
 * ------------------------------------------------------------------------- */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Makaretu.Dns;

using thZero.Responses;

namespace thZero.Services.Dns.Multicast
{
    public abstract class MakaretuMulticastDnsService<TService, TConfig> : ConfigServiceBase<TService, TConfig>, IMulticastDnsService
        where TConfig: class, IConfigDns
    {
        public MakaretuMulticastDnsService(IOptions<TConfig> config, ILogger<TService> logger) : base(config, logger)
        {
        }

        #region Public Methods
        public SuccessResponse Initialize()
        {
            try
            {
                if (Config.Dns == null)
                    return Success(false);
                if (!Config.Dns.Enabled)
                    return Success(false);
                if (!Config.Dns.Local)
                    return Success(false);
                if (String.IsNullOrEmpty(Config.Dns.Label))
                    return Success(false);

                var service = Config.Dns.Label + Local;

                Logger.LogInformation("Registered {service} as Multicast DNS...", service);

                //var addresses = MulticastService.GetIPAddresses().Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                var addresses = GetIPAddresses().Where(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                ICollection<ResourceRecord> records = new List<ResourceRecord>();
                foreach (var address in addresses)
                {
                    records.Add(new ARecord
                    {
                        Name = service,
                        Address = address
                    });
                }

                var mdns = new MulticastService();
                mdns.QueryReceived += (s, e) =>
                {
                    try
                    {
                        if (!e.Message.Questions.Any(q => q.Name == service))
                            return;

                        Logger.LogDebug("...query for {service}", service);

                        Message res = e.Message.CreateResponse();
                        res.Answers.AddRange(records);
                        mdns.SendAnswer(res);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, null);
                    }
                };
                mdns.Start();

                return Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, null);
            }

            return Error();
        }

        public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToArray();
            if (nics.Length > 0)
                return nics;

            // Special case: no operational NIC, then use loopbacks.
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up);
        }

        public static IEnumerable<IPAddress> GetIPAddresses()
        {
            return GetNetworkInterfaces()
                .Where(nic => nic.GetIPProperties().GatewayAddresses.Count > 0)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }
        #endregion

        #region Constants
        private const string Local = ".local";
        #endregion
    }
}
