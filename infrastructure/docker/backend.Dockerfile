FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
ARG PROJECT=Velqora.Api
WORKDIR /source
COPY . .
RUN dotnet publish src/backend/$PROJECT/$PROJECT.csproj -c Release -o /out /p:UseAppHost=false
RUN printf '%s' "$PROJECT" > /out/project
FROM mcr.microsoft.com/dotnet/aspnet:10.0.8
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /app/keys && chown -R app:app /app
USER app
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet $(cat /app/project).dll"]
