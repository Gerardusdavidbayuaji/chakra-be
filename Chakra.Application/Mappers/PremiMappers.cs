using Chakra.Application.Features.Premi.Dtos;
using Chakra.Domain.Entities;
using Mapster;

namespace Chakra.Application.Mappers;

public static class PremiMappers
{
    public static PremiResponseDto ToResponseDto(this Premi premi) =>
        premi.Adapt<PremiResponseDto>()!;
}