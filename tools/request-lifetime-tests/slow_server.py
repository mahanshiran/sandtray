from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
import time
class Slow(BaseHTTPRequestHandler):
 def do_GET(self):
  time.sleep(10)
  try:self.send_response(200);self.end_headers();self.wfile.write(b'ok')
  except (BrokenPipeError,ConnectionResetError):pass
 do_POST=do_GET
 def log_message(self,*args):pass
ThreadingHTTPServer(('127.0.0.1',18764),Slow).serve_forever()
