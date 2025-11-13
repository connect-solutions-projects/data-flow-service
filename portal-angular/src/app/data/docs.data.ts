export interface DocLink {
  title: string;
  description: string;
  icon: string;
  route: string;
  tags?: string[];
}

export interface DocSection {
  id: string;
  title: string;
  gradientClass?: string;
  cards: DocLink[];
}

export const docSections: DocSection[] = [
  {
    id: 'inicio-rapido',
    title: 'Início Rápido',
    gradientClass: 'features--blue',
    cards: [
      {
        title: '1. Instalar Docker',
        description: 'Configure Docker e Docker Compose no seu ambiente',
        icon: 'fas fa-download',
        route: '/docs/operations/instalacao-docker'
      },
      {
        title: '2. Subir Infraestrutura',
        description: 'Execute PostgreSQL, Redis, RabbitMQ e observabilidade',
        icon: 'fas fa-server',
        route: '/docs/operations/docker-compose'
      },
      {
        title: '3. Subir Aplicações',
        description: 'Inicie API, Worker e Reporting Service',
        icon: 'fas fa-cogs',
        route: '/docs/operations/manual-completo'
      },
      {
        title: '4. Testar Sistema',
        description: 'Faça upload e valide o processamento assíncrono',
        icon: 'fas fa-check-circle',
        route: '/docs/tutorials/endpoints-api'
      }
    ]
  },
  {
    id: 'arquitetura',
    title: 'Arquitetura',
    gradientClass: 'features--teal',
    cards: [
      {
        title: 'Arquitetura Técnica',
        description: 'Visão detalhada de componentes e fluxos do sistema',
        icon: 'fas fa-diagram-project',
        route: '/docs/architecture/arquitetura-tecnica'
      },
      {
        title: 'Decisões Técnicas',
        description: 'Justificativas das escolhas tecnológicas do projeto',
        icon: 'fas fa-lightbulb',
        route: '/docs/architecture/decisoes-tecnicas'
      }
    ]
  },
  {
    id: 'operacoes',
    title: 'Operações',
    gradientClass: 'features--purple',
    cards: [
      {
        title: 'Instalação Docker',
        description: 'Guia completo para instalar Docker e Docker Compose',
        icon: 'fas fa-download',
        route: '/docs/operations/instalacao-docker'
      },
      {
        title: 'Docker Compose',
        description: 'Como usar os perfis de infraestrutura e aplicações',
        icon: 'fas fa-docker',
        route: '/docs/operations/docker-compose'
      },
      {
        title: 'Manual Completo',
        description: 'Checklist de instalação, testes e observabilidade',
        icon: 'fas fa-book-open',
        route: '/docs/operations/manual-completo'
      },
      {
        title: 'Deploy Docker Hub',
        description: 'Automatize build e push das imagens para o Hub',
        icon: 'fas fa-cloud-upload-alt',
        route: '/docs/operations/docker-hub'
      },
      {
        title: 'Tag & Push Docker',
        description: 'Tagueie e publique imagens geradas via docker compose',
        icon: 'fas fa-tags',
        route: '/docs/operations/tag-push-docker'
      }
    ]
  }
];

export const tutorialCards: DocLink[] = [
  {
    title: 'Endpoints da API',
    description: 'Como usar os endpoints da API DataFlow para ingestão de arquivos',
    icon: 'fas fa-plug',
    route: '/docs/tutorials/endpoints-api',
    tags: ['REST API', 'Swagger', '.NET 9']
  },
  {
    title: 'Grafana & Prometheus',
    description: 'Monitoramento e observabilidade completos da plataforma',
    icon: 'fas fa-chart-line',
    route: '/docs/tutorials/grafana-prometheus',
    tags: ['Grafana', 'Prometheus', 'Métricas']
  },
  {
    title: 'Scripts do Projeto',
    description: 'Utilitários para certificados, ingestão e diagramas',
    icon: 'fas fa-terminal',
    route: '/docs/tutorials/scripts',
    tags: ['PowerShell', 'Batch', 'Automação']
  },
  {
    title: 'Executar Fora do Docker',
    description: 'Configure o ambiente manualmente sem containers',
    icon: 'fas fa-server',
    route: '/docs/tutorials/executar-fora-docker',
    tags: ['.NET 9', 'Local', 'Desenvolvimento']
  },
  {
    title: 'OpenSSL no PATH',
    description: 'Guia para habilitar o OpenSSL em ambientes Windows',
    icon: 'fas fa-key',
    route: '/docs/tutorials/adicionar-openssl-path',
    tags: ['OpenSSL', 'Windows']
  }
];

export const templateDocs: DocLink[] = [
  {
    title: 'Modelo de Relatório Final',
    description: 'Template para consolidar resultados de execução e observabilidade',
    icon: 'fas fa-file-signature',
    route: '/docs/templates/modelo-relatorio',
    tags: ['Template', 'Relatórios']
  }
];

