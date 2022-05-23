#!/bin/bash

dotnet publish -c Release -o bin/publish

docker build . -t cite/api --no-cache

docker-compose up -d