namespace Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;

public record GetCitiesResponse
(
    int Id,
    string Name,
    string NameEn,
    int StateId,
    string State
);