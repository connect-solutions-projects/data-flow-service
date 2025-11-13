import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { HomeComponent } from './components/home/home.component';
import { DocViewerComponent } from './components/doc-viewer/doc-viewer.component';

const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'docs/:category/:slug', component: DocViewerComponent },
  { path: '**', redirectTo: '' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' })],
  exports: [RouterModule]
})
export class AppRoutingModule { }

