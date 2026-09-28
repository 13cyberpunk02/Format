FROM debian:bookworm-slim

# cups - сервер печати, cups-filters - конвертация PDF,
# printer-driver-cups-pdf - виртуальный PDF-принтер, poppler-utils - утилита pdfinfo для проверки
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      cups cups-client cups-filters printer-driver-cups-pdf poppler-utils \
 && rm -rf /var/lib/apt/lists/*

# Разрешаем подключаться к CUPS не только изнутри контейнера (только для разработки!)
RUN sed -i 's/Listen localhost:631/Listen 0.0.0.0:631/' /etc/cups/cupsd.conf \
 && sed -i 's/<Location \/>/<Location \/>\n  Allow All/' /etc/cups/cupsd.conf \
 && sed -i 's/<Location \/admin>/<Location \/admin>\n  Allow All/' /etc/cups/cupsd.conf \
 && echo "ServerAlias *" >> /etc/cups/cupsd.conf \
 && echo "DefaultEncryption Never" >> /etc/cups/cupsd.conf

# cups-pdf будет складывать «напечатанное» в одну папку
RUN mkdir -p /var/spool/cups-pdf/out && chmod 777 /var/spool/cups-pdf/out \
 && sed -i 's|^#\?Out .*|Out /var/spool/cups-pdf/out|' /etc/cups/cups-pdf.conf \
 && sed -i 's|^#\?AnonDirName .*|AnonDirName /var/spool/cups-pdf/out|' /etc/cups/cups-pdf.conf

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 631
ENTRYPOINT ["/entrypoint.sh"]