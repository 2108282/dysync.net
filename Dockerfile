#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 10101


RUN printf '%s\n' \
      "deb https://deb.debian.org/debian bookworm main contrib non-free non-free-firmware" \
      "deb https://security.debian.org/debian-security bookworm-security main contrib non-free non-free-firmware" \
      "deb https://deb.debian.org/debian bookworm-updates main contrib non-free non-free-firmware" \
      "deb https://deb.debian.org/debian bookworm-backports main contrib non-free non-free-firmware" \
      > /etc/apt/sources.list && \
    apt-get -o Acquire::Retries=5 update && \
    apt-get -o Acquire::Retries=5 install -y --no-install-recommends ffmpeg intel-media-va-driver-non-free mesa-va-drivers vainfo && \
    rm -rf /var/lib/apt/lists/*

RUN ffmpeg -version

COPY . .
ENV ASPNETCORE_URLS=http://*:10101
ENV TZ=Asia/Shanghai
ENTRYPOINT ["dotnet", "dy.net.dll"]
