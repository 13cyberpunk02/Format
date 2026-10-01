#!/bin/sh
set -e

/usr/sbin/cupsd -f &
CUPSD_PID=$!
trap 'kill -TERM "$CUPSD_PID"' TERM INT

until lpstat -r >/dev/null 2>&1; do sleep 1; done

add_queue() {
  name="$1"
  uri="$2"
  model="$3"
  info="$4"

  if [ -z "$uri" ]; then
    echo "Очередь $name: адрес не задан, пропускаем"
    return
  fi

  while ! lpstat -p "$name" >/dev/null 2>&1; do
    if [ "$model" = "everywhere" ]; then
      lpadmin -p "$name" -E -v "$uri" -m everywhere -D "$info" && break
    else
      lpadmin -p "$name" -E -v "$uri" -P "$model" -D "$info" && break
    fi
    echo "Очередь $name: принтер $uri недоступен, повторим через 30 секунд"
    sleep 30
  done

  echo "Очередь $name готова ($uri)"
}

add_queue plotter "$PLOTTER_URI" "${PLOTTER_MODEL:-everywhere}" "${PLOTTER_INFO:-Плоттер}" &
add_queue office  "$OFFICE_URI"  "${OFFICE_MODEL:-everywhere}"  "${OFFICE_INFO:-Офисный принтер}" &

wait "$CUPSD_PID"