﻿using Newtonsoft.Json;

namespace Jojatekok.MoneroAPI.RpcManagers
{
    public class RpcResponse
    {
        [JsonProperty("status")]
        private string StatusString {
            set {
                switch (value.ToLower(Helper.InvariantCulture)) {
                    case "ok":
                        Status = RpcResponseStatus.Ok;
                        break;

                    case "busy":
                        Status = RpcResponseStatus.Busy;
                        break;

                    default:
                        Status = RpcResponseStatus.Error;
                        break;
                }
            }
        }
        public RpcResponseStatus Status { get; private set; }
    }
}