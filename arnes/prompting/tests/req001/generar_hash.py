"""Genera un hash compatible con PasswordHasher de ASP.NET Core Identity (V3)."""
import base64
import getpass
import hashlib
import secrets
import struct


def generar_hash(password):
    salt = secrets.token_bytes(16)
    iterations = 100_000
    subkey = hashlib.pbkdf2_hmac("sha512", password.encode("utf-8"), salt, iterations, 32)
    return base64.b64encode(b"\x01" + struct.pack(">III", 2, iterations, len(salt)) + salt + subkey).decode("ascii")


if __name__ == "__main__":
    print(generar_hash(getpass.getpass("Contraseña (no se muestra): ")))
