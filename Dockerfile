FROM node:20-bullseye AS frontend
WORKDIR /src
COPY package.json yarn.lock ./
RUN yarn install --frozen-lockfile
COPY frontend ./frontend
COPY webpack.* babel.* tsconfig.json ./
RUN yarn --cwd frontend run build --env production

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG TARGETARCH
ENV TARGETRID=linux-${TARGETARCH:-x64}
WORKDIR /src
COPY . .
COPY --from=frontend /src/_output/UI ./_output/UI
RUN if [ "$TARGETRID" = "linux-amd64" ]; then export TARGETRID=linux-x64; fi; \
    dotnet publish src/NzbDrone.Console/Readarr.Console.csproj -c Release -r ${TARGETRID} --self-contained -p:RunAnalyzers=false -o /app/publish && \
    cp -r _output/UI /app/publish/UI

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libsqlite3-0 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish/ .
EXPOSE 7337
ENTRYPOINT ["/app/Readarr", "--nobrowser", "--data=/data"]
