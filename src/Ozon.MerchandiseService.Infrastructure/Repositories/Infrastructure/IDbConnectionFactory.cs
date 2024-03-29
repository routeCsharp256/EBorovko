﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ozon.MerchandiseService.Infrastructure.Repositories.Infrastructure
{
    public interface IDbConnectionFactory<TConnection> : IDisposable
    {
        Task<TConnection> CreateConnection(CancellationToken token);
    }
}