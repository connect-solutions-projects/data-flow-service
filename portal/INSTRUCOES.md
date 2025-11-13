# 📋 Instruções do Portal

## ✅ O que foi criado

1. **Estrutura completa do portal** na pasta `portal/`
2. **Design moderno** com tema escuro e navegação lateral
3. **Páginas HTML** convertidas dos principais documentos
4. **CSS responsivo** que funciona em mobile e desktop
5. **JavaScript** para navegação e funcionalidades

## 🎨 Design

- Tema escuro moderno
- Cores: Gradiente roxo/azul (#6366f1, #8b5cf6)
- Sidebar fixa com navegação
- Cards e componentes estilizados
- Responsivo para mobile

## 📄 Páginas Criadas

### Completas:
- ✅ `index.html` - Página inicial
- ✅ `operations/instalacao-docker.html` - Guia completo de instalação
- ✅ `operations/docker-compose.html` - Guia Docker Compose
- ✅ `architecture/arquitetura-tecnica.html` - Arquitetura técnica

### Para criar (páginas placeholder):
- `architecture/decisoes-tecnicas.html`
- `operations/manual-completo.html`
- `operations/docker-hub.html`
- `operations/tag-push-docker.html`
- `tutorials/endpoints-api.html`
- `tutorials/grafana-prometheus.html`
- `tutorials/scripts.html`
- `tutorials/executar-fora-docker.html`
- `templates/modelo-relatorio.html`

## 🚀 Como Usar

1. Abra `portal/index.html` no navegador
2. Ou sirva via HTTP: `python -m http.server 8000` dentro da pasta `portal`
3. Navegue pelos links na sidebar

## 🔄 Para Converter Mais Documentos

Os documentos Markdown originais estão em `docs/`. Para converter mais documentos:

1. Leia o arquivo `.md`
2. Crie um novo arquivo `.html` na pasta correspondente
3. Use o mesmo template das páginas existentes
4. Converta o Markdown para HTML manualmente ou use uma ferramenta

## 📝 Template de Página

Todas as páginas seguem este template:

```html
<!DOCTYPE html>
<html lang="pt-BR">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Título - DataFlow</title>
    <link rel="stylesheet" href="../css/style.css">
</head>
<body>
    <nav class="sidebar">
        <!-- Navegação (copiar de index.html) -->
    </nav>
    <main class="main-content">
        <div class="content-page">
            <h1>Título</h1>
            <!-- Conteúdo aqui -->
        </div>
    </main>
    <script src="../js/main.js"></script>
</body>
</html>
```

## 🎯 Próximos Passos

1. Converter os documentos restantes de Markdown para HTML
2. Adicionar mais funcionalidades JavaScript se necessário
3. Personalizar cores e estilos conforme necessário
4. Deploy em GitHub Pages ou Netlify se desejar

---

**Portal criado e funcional!** 🎉

