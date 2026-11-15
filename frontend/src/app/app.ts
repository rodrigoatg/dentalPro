import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly displayName = computed(() => {
    const user = this.auth.user();
    return user ? `${user.firstName} ${user.lastName}`.trim() || user.userName : '';
  });
  protected readonly initials = computed(() => {
    const user = this.auth.user();
    return user ? `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`.toLocaleUpperCase('es-AR') : 'DP';
  });
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
    if (this.router.url.startsWith('/admin')) return 'Administración';
    if (this.router.url.startsWith('/appointments')) return 'Agenda';
    return this.router.url.startsWith('/patients') ? 'Pacientes' : 'Dashboard';
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigate(['/login']);
  }
}
