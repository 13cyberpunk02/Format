FROM debian:bookworm-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      cups cups-client cups-filters cups-ipp-utils ghostscript poppler-utils ca-certificates perl \
 && rm -rf /var/lib/apt/lists/*

RUN sed -i 's/Listen localhost:631/Listen 0.0.0.0:631/' /etc/cups/cupsd.conf \
 && sed -i 's/<Location \/>/<Location \/>\n  Allow All/' /etc/cups/cupsd.conf \
 && sed -i 's/<Location \/admin>/<Location \/admin>\n  Allow All/' /etc/cups/cupsd.conf \
 && echo "ServerAlias *" >> /etc/cups/cupsd.conf \
 && echo "DefaultEncryption Never" >> /etc/cups/cupsd.conf

COPY ppd/ /usr/share/cups/model/format/

COPY filters/ /usr/lib/cups/filter/
RUN find /usr/lib/cups/filter -maxdepth 1 \( -name '*.pl' -o -name '*.pm' \) -exec sed -i 's/\r$//' {} + \
 && find /usr/lib/cups/filter -maxdepth 1 -name '*.pl' -exec chmod 755 {} + \
 && find /usr/lib/cups/filter -maxdepth 1 -name '*.pm' -exec chmod 644 {} +

COPY entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh

EXPOSE 631
ENTRYPOINT ["/entrypoint.sh"]