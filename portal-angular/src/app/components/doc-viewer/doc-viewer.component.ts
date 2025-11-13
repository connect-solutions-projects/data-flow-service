import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, NavigationEnd, ParamMap, Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { HttpClient } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-doc-viewer',
  templateUrl: './doc-viewer.component.html',
  styleUrls: ['./doc-viewer.component.scss']
})
export class DocViewerComponent implements OnInit, OnDestroy {
  content: SafeHtml | null = null;
  isLoading = true;
  error: string | null = null;
  private routeSub?: Subscription;
  private navigationSub?: Subscription;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly http: HttpClient,
    private readonly sanitizer: DomSanitizer
  ) {}

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => this.loadDoc(params));
    this.navigationSub = this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => window.scrollTo({ top: 0, behavior: 'smooth' }));
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.navigationSub?.unsubscribe();
  }

  private loadDoc(params: ParamMap): void {
    const category = params.get('category');
    const slug = params.get('slug');

    if (!category || !slug) {
      this.handleError('Documento inválido.');
      return;
    }

    const url = `assets/docs/${category}/${slug}.html`;
    this.isLoading = true;
    this.error = null;

    this.http.get(url, { responseType: 'text' }).subscribe({
      next: html => {
        const parser = new DOMParser();
        const doc = parser.parseFromString(html, 'text/html');
        const contentElement = doc.querySelector('.content-page');
        const inner = contentElement ? contentElement.innerHTML : html;

        this.content = this.sanitizer.bypassSecurityTrustHtml(inner);
        this.isLoading = false;
      },
      error: () => {
        this.handleError('Documento não encontrado. Verifique o caminho informado.');
      }
    });
  }

  private handleError(message: string): void {
    this.isLoading = false;
    this.content = null;
    this.error = message;
  }
}

