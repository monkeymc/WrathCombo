#!/bin/bash

# Build the debug version and output to the G drive 'fun' folder
# Adjust the path if your G drive is mounted differently (e.g. /cygdrive/g or /media/g)
DEPLOY_PATH="/mnt/g/fun/WrathCombo"

echo "Building WrathCombo in Debug mode and deploying to $DEPLOY_PATH..."

dotnet build -c Debug /home/chatja/fun/WrathCombo/WrathCombo/WrathCombo.csproj -o "$DEPLOY_PATH"

if [ $? -eq 0 ]; then
    echo "Deployment to $DEPLOY_PATH successful!"
else
    echo "Build failed!"
    exit 1
fi
