FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /workspace

COPY . .

RUN dotnet restore src/DentalPro.Api/DentalPro.Api.csproj

EXPOSE 8080

CMD ["dotnet", "watch", "--project", "src/DentalPro.Api", "run", "--no-launch-profile"]
