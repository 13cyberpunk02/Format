#!/bin/sh
set -e

# Запускаем CUPS в фоне, чтобы успеть создать очереди
/usr/sbin/cupsd -f &
CUPSD_PID=$!

# При остановке контейнера корректно гасим CUPS
trap 'kill -TERM "$CUPSD_PID"' TERM INT

# Ждём, пока CUPS поднимется
until lpstat -r >/dev/null 2>&1; do sleep 1; done

PPD=/usr/share/ppd/cups-pdf/CUPS-PDF_opt.ppd

# Создаём очереди, если их ещё нет
lpstat -p plotter >/dev/null 2>&1 || \
  lpadmin -p plotter -E -v cups-pdf:/ -P "$PPD" -D "Плоттер (виртуальный, dev)"

lpstat -p office >/dev/null 2>&1 || \
  lpadmin -p office -E -v cups-pdf:/ -P "$PPD" -D "Офисный принтер (виртуальный, dev)"

echo "CUPS готов. Очереди:"
lpstat -p

# Ждём, пока CUPS работает - контейнер живёт, пока жив CUPS
wait "$CUPSD_PID"