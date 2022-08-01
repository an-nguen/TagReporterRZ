FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
RUN apt update && apt install -y libgdiplus
RUN apt install -y cifs-utils
RUN mkdir -p /mnt/share
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["TagReporter.csproj", "TagReporter/"]
RUN dotnet restore "TagReporter/TagReporter.csproj"
COPY . ./TagReporter
WORKDIR "/src/TagReporter"
RUN dotnet build "TagReporter.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TagReporter.csproj" -c Release -o /app/publish

FROM base AS final
ENV TZ=Europe/Moscow
ENV LANG ru_RU.UTF-8
ENV LANGUAGE ${LANG}
ENV LC_ALL ${LANG}
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TagReporter.dll"]
