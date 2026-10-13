import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly router = inject(Router);
  protected readonly sectionTitle = signal(this.currentSection());
  protected readonly year = new Date().getFullYear();
  protected readonly todayLabel = new Intl.DateTimeFormat('es-AR', { dateStyle: 'full' }).format(
    new Date(),
  );

  constructor() {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      this.sectionTitle.set(this.currentSection());
    });
  }

  private currentSection(): string {
    return this.router.url.startsWith('/patients') ? 'Pacientes' : 'Dashboard';
  }
}
