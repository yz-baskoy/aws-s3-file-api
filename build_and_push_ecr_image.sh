#!/bin/bash
set -e

aws ecr get-login-password --region us-east-1 --profile Yzb | docker login --username AWS --password-stdin 
