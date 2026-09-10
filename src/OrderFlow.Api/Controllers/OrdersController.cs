// Controllers/OrdersController.cs
namespace OrderFlow.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using OrderFlow.Api.Contracts;
using OrderFlow.Application.Common.Models;
using OrderFlow.Application.Orders;
using OrderFlow.Application.Orders.Dtos;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>Places a new order.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request, CancellationToken ct)
    {
        var id = await orders.PlaceOrderAsync(request.ToInput(), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    /// <summary>Gets a single order by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await orders.GetOrderByIdAsync(id, ct));

    /// <summary>Lists orders, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default) =>
        Ok(await orders.ListOrdersAsync(page, pageSize, status, ct));

    /// <summary>Cancels an order. Idempotent.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct)
    {
        await orders.CancelOrderAsync(id, request.Reason, ct);
        return NoContent();
    }
}
