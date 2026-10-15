using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Inventory;
using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-IMP-001 §12.4 (US-IMP-01) -- suppliers. A supplier is company-wide, not branch-scoped.</summary>
[ApiController]
[Route("api/staff/procurement/suppliers")]
public sealed class ProcurementSuppliersController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] string? countryCode, [FromQuery] bool? isActive, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetSuppliersQuery
        {
            Search = search, CountryCode = countryCode, IsActive = isActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };
        if (PagingHelper.ApplyPaging(query, pageNumber, pageSize) is { } invalid) return invalid;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpPost]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
    {
        var supplierId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new CreateSupplierCommand
        {
            SupplierId = supplierId, Code = request.Code, Name = request.Name, CountryCode = request.CountryCode, ContactName = request.ContactName,
            Phone = request.Phone, Email = request.Email, Address = request.Address, CurrencyCode = request.CurrencyCode, LeadTimeDays = request.LeadTimeDays,
            PaymentTerms = request.PaymentTerms, PaymentTermDays = request.PaymentTermDays, Notes = request.Notes,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.CreatedOrFailure(response, new { supplierId });
    }

    [HttpGet("{id:guid}")]
    [RequireAction(Actions.Procurement.View)]
    public async Task<IActionResult> Get(Guid id)
    {
        var response = await dispatcher.GetResultAsync(new GetSupplierQuery
        { SupplierId = id, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{id:guid}")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierRequest request)
    {
        var response = await dispatcher.SendAsync(new UpdateSupplierCommand
        {
            SupplierId = id, Name = request.Name, CountryCode = request.CountryCode, ContactName = request.ContactName, Phone = request.Phone,
            Email = request.Email, Address = request.Address, CurrencyCode = request.CurrencyCode, LeadTimeDays = request.LeadTimeDays,
            PaymentTerms = request.PaymentTerms, PaymentTermDays = request.PaymentTermDays, Notes = request.Notes,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }

    [HttpPut("{id:guid}/activation")]
    [RequireAction(Actions.Procurement.Manage)]
    public async Task<IActionResult> SetActivation(Guid id, [FromBody] SetSupplierActivationRequest request)
    {
        var response = await dispatcher.SendAsync(new SetSupplierActivationCommand
        {
            SupplierId = id, IsActive = request.IsActive,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        });

        return this.NoContentOrFailure(response);
    }
}
