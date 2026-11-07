import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isLogin = request.url.split('?')[0].endsWith('/api/auth/login');
  const token = auth.accessToken();
  const authorizedRequest = token && !isLogin
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isLogin) {
        const returnUrl = router.url;
        auth.clearSession();
        void router.navigate(['/login'], { queryParams: { returnUrl } });
      } else if (error instanceof HttpErrorResponse && error.status === 403) {
        auth.reportForbidden();
      }
      return throwError(() => error);
    }),
  );
};
