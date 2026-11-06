"""Proxy CONNECT de prueba: retrasa TCP; nunca descifra TLS ni registra cabeceras/cuerpos."""
import select
import socket
import socketserver
import time


class ConexionRetardada(socketserver.StreamRequestHandler):
    def handle(self):
        primera = self.rfile.readline(4096).decode("ascii", "replace").split()
        if not primera:
            return
        while True:
            cabecera = self.rfile.readline(8192)
            if cabecera in (b"\r\n", b"\n", b""):
                break
        if len(primera) < 2 or primera[0] != "CONNECT" or primera[1] != "generativelanguage.googleapis.com:443":
            self.wfile.write(b"HTTP/1.1 502 Bad Gateway\r\n\r\n")
            return
        print("CONNECT Google recibido; retardo de prueba 45 segundos", flush=True)
        time.sleep(45)
        try:
            with socket.create_connection(("generativelanguage.googleapis.com", 443), timeout=10) as destino:
                self.wfile.write(b"HTTP/1.1 200 Connection established\r\n\r\n")
                self.wfile.flush()
                while True:
                    legibles, _, _ = select.select([self.connection, destino], [], [], 5)
                    for origen in legibles:
                        datos = origen.recv(65536)
                        if not datos:
                            return
                        (destino if origen is self.connection else self.connection).sendall(datos)
        except (OSError, TimeoutError):
            pass


class ServidorRetardo(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


with ServidorRetardo(("0.0.0.0", 8080), ConexionRetardada) as servidor:
    print("Proxy de retardo req009 disponible; solo Google:443, sin descifrar TLS", flush=True)
    servidor.serve_forever()
