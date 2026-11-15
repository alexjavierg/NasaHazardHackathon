#!/bin/bash
echo "Descargando últimos cambios de GitHub..."
git pull origin main
echo "Reconstruyendo y levantando contenedor en el puerto 80..."
sudo docker compose up -d --build
echo "¡Listo! API actualizada en http://13.78.132.29"
