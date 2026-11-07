import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  imports: [RouterLink],
  template: `
    <section class="access-card">
      <span class="eyebrow">ACCESO RESTRINGIDO</span>
      <h1>No tenés permiso para ver esta sección</h1>
      <p>Tu cuenta no tiene asignado el permiso necesario. Si lo necesitás, consultá con un administrador.</p>
      <a routerLink="/dashboard">Volver al inicio</a>
    </section>
  `,
  styles: `
    .access-card { max-width: 620px; margin: 72px auto; padding: 38px; border: 1px solid #e5ebef; border-radius: 16px; background: white; }
    .eyebrow { color: #718498; font-size: 10px; font-weight: 750; letter-spacing: 1.2px; }
    h1 { margin: 12px 0 8px; color: #18344d; font-size: 25px; }
    p { color: #778797; line-height: 1.6; }
    a { color: #267b76; font-weight: 650; }
  `,
})
export class Forbidden {}
