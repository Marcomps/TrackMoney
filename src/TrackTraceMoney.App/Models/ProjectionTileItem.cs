using TrackTraceMoney.Application.Reporting;
using TrackTraceMoney.Domain.Enums;

namespace TrackTraceMoney.App.Models;

/// <summary>One currency's row pair in the Dashboard projection tile: through this quincena and through month end.</summary>
public sealed record ProjectionTileItem(CurrencyCode Currency, CashFlowProjection HalfMonth, CashFlowProjection Month);
