FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS runtime-image

COPY . /src
RUN cd src && \
    dotnet publish *.csproj --output /output

from mcr.microsoft.com/dotnet/core/aspnet:3.1

COPY --from=runtime-image ["/output", "/output"]

CMD ["dotnet", "/output/ExternalScalerSample.dll"]