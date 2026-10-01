set -e

NAME="${1:-company.ru}"
cd /certs

# Центр сертификации — создаётся один раз, на 10 лет
if [ ! -f ca.key ]; then
  openssl req -x509 -new -nodes -newkey rsa:3072 -sha256 -days 3650 \
    -keyout ca.key -out ca.crt \
    -subj "/CN=Format Internal CA"
  echo "Создан центр сертификации ca.crt"
fi

openssl req -new -nodes -newkey rsa:2048 \
  -keyout server.key -out server.csr \
  -subj "/CN=$NAME"

cat > server.ext <<EOF
basicConstraints = CA:FALSE
keyUsage = digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = DNS:$NAME, DNS:localhost, IP:127.0.0.1
EOF

openssl x509 -req -in server.csr \
  -CA ca.crt -CAkey ca.key -CAcreateserial \
  -sha256 -days 825 \
  -out server.crt -extfile server.ext

rm server.csr server.ext
echo "Готово: server.crt для $NAME, подписан ca.crt"