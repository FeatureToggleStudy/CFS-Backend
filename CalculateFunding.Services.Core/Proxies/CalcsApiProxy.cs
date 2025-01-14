﻿using CalculateFunding.Services.Core.Interfaces.Logging;
using CalculateFunding.Services.Core.Interfaces.Proxies;
using CalculateFunding.Services.Core.Options;
using Serilog;

namespace CalculateFunding.Services.Core.Proxies
{
    public class CalcsApiProxy : ApiClientProxy, ICalcsApiClientProxy
    {
        public CalcsApiProxy(ApiOptions options, ILogger logger, ICorrelationIdProvider correlationIdProvider) : base(options, logger, correlationIdProvider)
        {
        }
    }
}
