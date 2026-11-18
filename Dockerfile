FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BubbleSortMvc.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-venv \
    && rm -rf /var/lib/apt/lists/* \
    && python3 -m venv /opt/venv
COPY requirements.txt /tmp/requirements.txt
RUN arch="$(uname -m)" \
    && case "$arch" in \
         x86_64) checksum=cb9a8ba8137ab24e36bf1742cb79a1294bd374db570f09fc15a5e1318160db4e ;; \
         aarch64) checksum=610f600c102386e581327d5efc18c0d6edecb9820b4140d26163354a99cd800d ;; \
         *) echo "Unsupported CPU architecture: $arch" >&2; exit 1 ;; \
       esac \
    && wheel="torch-2.8.0+cpu-cp312-cp312-manylinux_2_28_${arch}.whl" \
    && /opt/venv/bin/python -c 'import sys, urllib.request; urllib.request.urlretrieve(sys.argv[1], sys.argv[2])' \
         "https://download.pytorch.org/whl/cpu/torch-2.8.0%2Bcpu-cp312-cp312-manylinux_2_28_${arch}.whl" "/tmp/$wheel" \
    && echo "$checksum  /tmp/$wheel" | sha256sum -c - \
    && /opt/venv/bin/pip install --no-cache-dir "/tmp/$wheel" \
    && rm "/tmp/$wheel" \
    && /opt/venv/bin/pip install --no-cache-dir -r /tmp/requirements.txt \
    && rm /tmp/requirements.txt \
    && mkdir -p /data/keys \
    && chown -R app:app /data
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080 \
    PythonExecutable=/opt/venv/bin/python \
    OMP_NUM_THREADS=1 \
    OPENBLAS_NUM_THREADS=1
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "BubbleSortMvc.dll"]
