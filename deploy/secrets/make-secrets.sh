#!/bin/sh
set -e

ADMIN_EMAIL="${1:?Укажите почту первого администратора}"
cd /secrets

random() {
  openssl rand -base64 64 | tr -dc 'A-Za-z0-9' | head -c "$1"
}

write() {
  if [ -f "$1" ]; then
    echo "есть:    $1"
  else
    printf '%s' "$2" > "$1"
    chmod 644 "$1"
    echo "создан:  $1"
  fi
}

write postgres_password "$(random 32)"
PG_PASSWORD="$(cat postgres_password)"

write ConnectionStrings__Auth    "Host=postgres;Port=5432;Database=format_auth;Username=format;Password=$PG_PASSWORD"
write ConnectionStrings__Storage "Host=postgres;Port=5432;Database=format_storage;Username=format;Password=$PG_PASSWORD"
write ConnectionStrings__Print   "Host=postgres;Port=5432;Database=format_print;Username=format;Password=$PG_PASSWORD"

write S3__AccessKey "format_$(random 16)"
write S3__SecretKey "$(random 40)"
ACCESS_KEY="$(cat S3__AccessKey)"
SECRET_KEY="$(cat S3__SecretKey)"

write s3config.json "{
  \"identities\": [
    {
      \"name\": \"format-storage\",
      \"credentials\": [ { \"accessKey\": \"$ACCESS_KEY\", \"secretKey\": \"$SECRET_KEY\" } ],
      \"actions\": [\"Read\", \"Write\", \"List\"]
    }
  ]
}"

if [ -f jwt-signing.pem ]; then
  echo "есть:    jwt-signing.pem"
else
  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out jwt-signing.pem
  chmod 644 jwt-signing.pem
  echo "создан:  jwt-signing.pem"
fi

write BootstrapAdmin__Email "$ADMIN_EMAIL"
write BootstrapAdmin__Password "$(random 16)"

echo
echo "Первый администратор: $(cat BootstrapAdmin__Email)"
echo "Пароль:               $(cat BootstrapAdmin__Password)"
echo "Смените пароль в профиле после первого входа."