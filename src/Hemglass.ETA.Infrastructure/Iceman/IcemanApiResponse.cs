namespace Hemglass.ETA.Infrastructure.Iceman;

public record IcemanApiResponse(
    int StatusCode,
    string Message,
    List<IcemanStop> Data
);

public record IcemanStop(
    int StopId,
    double Longitude,
    double Latitude,
    string StreetAddress,
    string StreetNumber,
    string NextTime,
    DateTime NextDate
);
