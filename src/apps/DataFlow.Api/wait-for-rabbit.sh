#!/usr/bin/env sh
set -e

RABBIT_USER="${RABBITMQ_USER:-admin}"
RABBIT_PASS="${RABBITMQ_PASSWORD:-supersecret_admin}"
RABBIT_HOST="${RABBITMQ_HOST:-rabbitmq}"
RABBIT_PORT="${RABBITMQ_MGMT_PORT:-15672}"

echo "⏳ Aguardando RabbitMQ em http://$RABBIT_HOST:$RABBIT_PORT ..."

while true; do
  # 1) aliveness-test do vhost raiz (/%2F) — valida AMQP pronto
  if resp=$(wget -qO- --user="$RABBIT_USER" --password="$RABBIT_PASS" \
      "http://$RABBIT_HOST:$RABBIT_PORT/api/aliveness-test/%2F" 2>/dev/null); then
    echo "$resp" | grep -q '"status":"ok"' && break
  fi

  # 2) health/ready (quando disponível)
  if resp=$(wget -qO- --user="$RABBIT_USER" --password="$RABBIT_PASS" \
      "http://$RABBIT_HOST:$RABBIT_PORT/api/health/ready" 2>/dev/null); then
    echo "$resp" | grep -q '"status":"ok"' && break
  fi

  # 3) healthchecks/node (nó saudável; pode anteceder AMQP)
  if resp=$(wget -qO- --user="$RABBIT_USER" --password="$RABBIT_PASS" \
      "http://$RABBIT_HOST:$RABBIT_PORT/api/healthchecks/node" 2>/dev/null); then
    echo "$resp" | grep -q 'ok' && break
  fi

  echo "RabbitMQ ainda não está pronto... aguardando 3s"
  sleep 3
done

echo "✅ RabbitMQ está pronto, iniciando a API..."
exec dotnet DataFlow.Api.dll
