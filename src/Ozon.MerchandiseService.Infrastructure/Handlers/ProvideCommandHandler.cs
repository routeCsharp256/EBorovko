﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Ozon.MerchandiseService.Domain.Aggregates.Employee;
using Ozon.MerchandiseService.Domain.Aggregates.Employee.ValueObjects;
using Ozon.MerchandiseService.Domain.Aggregates.MerchandiseProvidingRequest;
using Ozon.MerchandiseService.Domain.Contracts;
using Ozon.MerchandiseService.Infrastructure.Commands;
using Ozon.MerchandiseService.Infrastructure.Services.Interfaces;

namespace Ozon.MerchandiseService.Infrastructure.Handlers
{
    public class ProvideCommandHandler: IRequestHandler<ProvideCommand, ProvideCommandResponse>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMerchandiseProvidingRequestRepository _merchandiseProvidingRequestRepository;
        private readonly IStockApiService _stockApiService;
        private readonly IDateTimeService _dateTimeService;

        public ProvideCommandHandler(
            IUnitOfWork unitOfWork,
            IMerchandiseProvidingRequestRepository merchandiseProvidingRequestRepository,
            IStockApiService stockApiService,
            IDateTimeService dateTimeService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _merchandiseProvidingRequestRepository = merchandiseProvidingRequestRepository ?? throw new ArgumentNullException(nameof(merchandiseProvidingRequestRepository));
            _stockApiService = stockApiService ?? throw new ArgumentNullException(nameof(stockApiService));
            _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
            
        }
        
        public async Task<ProvideCommandResponse> Handle(ProvideCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.StartTransaction(cancellationToken);
            
            var merchRequest =  await _merchandiseProvidingRequestRepository.FindByMerchPackIdAndEmployeeEmailAsync(request.MerchPackId, request.EmployeeEmail, cancellationToken);
            
            if (merchRequest.IsDone && !merchRequest.CheckOneYearPassFromProviding(_dateTimeService.Now))
                throw new Exception("Merch pack of such type already provide to employee");
            
            if (merchRequest.InWork)
                throw new Exception("Request for мerch pack of such type exists. Waiting stock supplying");
            
            var newMerchRequest = new MerchandiseProvidingRequest(new Employee(0,Email.Create(request.EmployeeEmail)),  request.MerchPackId, request.ClothingSize, _dateTimeService.Now);

            var skuItems = await Task.WhenAll(newMerchRequest.SkuIds.Select(skuId => _stockApiService.GetByItemTypeAsync(skuId, request.ClothingSize)));

            bool areAllAvailable = skuItems.All(item => item.Quantity > 0);
            if (!areAllAvailable)
                newMerchRequest.Wait();
            else
            {
                var areGiveOut =  await _stockApiService.GiveOutItemsAsync(skuItems.Select(skuItem => skuItem.Id));
                if(areGiveOut)
                    newMerchRequest.Complete(_dateTimeService.Now);
                else
                    newMerchRequest.Wait();
            }
            
            long id = await _merchandiseProvidingRequestRepository.CreateAsync(newMerchRequest, cancellationToken);
            
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProvideCommandResponse
            {
                MerchProvidingRequestId = id,
                MerchPackId = newMerchRequest.MerchandisePackType.Id,
                Status = newMerchRequest.CurrentStatus.Name
            };
        }
    }
}