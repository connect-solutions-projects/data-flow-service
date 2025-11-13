import { Component, HostListener, OnDestroy } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Subscription } from 'rxjs';

interface NavLink {
  label: string;
  fragment?: string;
  route?: string;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnDestroy {
  isMobileMenuOpen = false;
  isScrolled = false;
  private navigationSub?: Subscription;

  navLinks: NavLink[] = [
    { label: 'Início', fragment: 'home', route: '/' },
    { label: 'Arquitetura', fragment: 'arquitetura', route: '/' },
    { label: 'Operações', fragment: 'operacoes', route: '/' },
    { label: 'Tutoriais', fragment: 'tutoriais', route: '/' },
    { label: 'Links', fragment: 'links', route: '/' }
  ];

  constructor(private readonly router: Router) {
    this.navigationSub = this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.isMobileMenuOpen = false;
      });
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen = false;
  }

  @HostListener('window:scroll')
  onWindowScroll(): void {
    this.isScrolled = window.scrollY > 10;
  }

  ngOnDestroy(): void {
    this.navigationSub?.unsubscribe();
  }
}

