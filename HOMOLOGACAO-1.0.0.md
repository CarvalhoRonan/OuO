# Validação do OuO 1.1.0 — Overclock's Utility Optimizer

Prévia 1 • 22/11/2026. SHA-256 do executável: 47604A8AC6C4FF5D1E6048866506CE466043B9787FA85F02CDECEBC3789FD337.

## Ensaios desta compilação

- **407 verificações automatizadas aprovadas.** Incluem os casos existentes e os novos testes de progresso real, canais do GitHub, metadados inválidos, ZIP com caminhos indevidos/duplicados, cancelamento, remoção de dados reconhecíveis do relatório, restauração por operação com conflitos e escrita repetida, pacotes de drivers e layout responsivo.
- **26 telas verificadas**, temas claro/escuro e português/inglês, com preservação de controles e resultados ao navegar e mudar aparência.
- **Novos controles verificados:** barra dos tweaks permanece fixa ao rolar; limpar um relatório recolhe a área vazia; falhas exibem Relatar problema; progresso contado de 1/4 mostra 25%; relógio devolve 00:59 e cancelamento preserva o valor.
- **38 verificações de aceitação aprovadas.** Consultas do Agendador, inventário Store, DNS efetivo e arquivos de teste próprios. A checagem F8 elevada não foi executada no Windows de trabalho.
- **Navegação de dez minutos:** PASS • 601,4 s; 8242 transitions through 26 views; 317 cycles; dark/light and Portuguese/English changes.
Managed heap after collection: 16989968 -> 17524584 bytes; retained growth 534616 bytes.
Process handles: 902 -> 902.
Ten-minute navigation stress test through the normal event loop; not long-term qualification with personal applications.
- **Consulta real do GitHub sem login:** passou. Foi encontrada a release pública 1.0.0.107; não havia release estável nesse momento. Canais e ordenação numérica também foram testados com dados controlados.
- **Atualizador com pacote real:** baixou a release 1.0.0.107 do repositório oficial, conferiu tamanho/SHA-256, guardou a candidata e extraiu a cópia baixada. O executável do backup corresponde a esta candidata; o executável extraído corresponde à release publicada. Foram recebidos 1181 avisos de progresso. Nenhuma cópia baixada foi aberta e nenhuma configuração foi modificada. Ativação da nova cópia, uso posterior e recuperação do aplicativo entre versões publicadas continuam pendentes.
- **Inventários reais somente de leitura:** 14 definições de navegadores, dois instalados nesta máquina; sete pacotes de vídeo/áudio elegíveis, nenhum removido; PawnIO 2.2.0 já instalado; consulta de reinicialização sem disparar reinício.
- **PawnIO 2.2.0:** instalador oficial com assinatura válida do editor namazso e SHA-256 1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032. Fontes correspondentes e licenças acompanham a distribuição. Não foi instalado ou atualizado por esta rodada.
- **Defender:** análise específica do executável com saída 0, concluída em UTC 2026-11-22T14:53:09.7356969Z. O ZIP recebe conferência própria após a criação. Esse resultado se limita aos arquivos e às definições disponíveis na rodada.

## Limites e critérios de liberação

Esta entrega **não qualifica o produto inteiro como estável**. Os 18 itens desta rodada estão implementados, com os limites descritos em NOVIDADES-1.1.0.md; a publicação do login Google ficou fora por decisão do proprietário. Código visível com licença restritiva e nome novo foram incluídos.

O computador de trabalho não foi usado para ensaios de remoção física de drivers, migração de Windows, alteração de boot, clonagem, recuperação de conta, instalação/remoção do Office ou reparos DISM/SFC. As evidências anteriores de ambientes descartáveis continuam sendo antecedentes, não uma homologação desses fluxos administrativos na compilação atual. MSI, Office e reparos permanecem prioritários.

Instalação limpa, atualização/recuperação, cancelamento, indisponibilidade de rede e falhas precisam de evidência por recurso. Compatibilidade com Windows 7/8/8.1/10, x86, outros fabricantes, leitores de tela e múltiplos monitores/DPI continua exigindo ensaios correspondentes. Novos navegadores sem política validada usam a loja assistida.

A distribuição permanece **sem assinatura de código**, por escolha do proprietário. O alerta Behavior:Win32/Persistence.A!ml da antiga 1.0.0.102 segue sem decisão registrada da Microsoft; a análise limpa desta compilação não explica aquele alerta.

## Grupos que conservam o aviso de testes

- **Atualização do próprio OuO:** Consulta, canais e integridade possuem testes. Troca e recuperação entre duas distribuições completas ainda precisam de ensaio.
- **Limpeza de vídeo e áudio:** Inventário, escopo e proteções possuem testes; remoção, reinicialização e reinstalação em hardware físico ainda precisam de validação.
- **Instalação do driver de sensores:** Instalador oficial 2.2.0 conferido por hash e assinatura; primeira instalação e cancelamento de UAC variam por sistema e precisam de ensaio próprio.
- **Clonagem e inicialização:** Cópia e GPT verificadas em discos virtuais; inicialização da cópia e dispositivos SATA/NVMe/USB ainda precisam de ensaio.
- **Recuperação de conta:** Cópias e formato do Registro foram verificados. A entrada na conta de um Windows recuperado ainda precisa de validação.
- **F8 e recuperação:** A alternância e a reinicialização precisam de um Windows instalado; o Sandbox não oferece BCD convencional.
- **Ponto de restauração:** A agenda foi validada. Criação efetiva e restauração dependem da Proteção do Sistema e de ensaio adicional.
- **Instalação e remoção do Office:** A ferramenta e os planos são conferidos. Edições, conflitos MSI/Click-to-Run e instalações reais ainda não têm homologação ampla.
- **Instalação de drivers:** Busca e correspondência foram verificadas. Instalação, reinicialização e versão final exigem hardware correspondente.
- **Download e remoção do OneDrive:** Proteções verificadas em arquivos controlados. Download e remoção em contas pessoais/empresariais ainda estão em validação.
- **Análise de armazenamento em nuvem:** Login, arquivos e cotas por serviço foram consultados em uma conta pessoal Google. Outras contas, OneDrive e cenários de falha ainda precisam de validação ampliada.
- **Gerenciamento de memória:** API e critérios verificados em auxiliares próprios. Acompanhamento prolongado com aplicativos reais ainda está em validação.
- **Recuperação de impressão:** Execução e proteções verificadas em cenários controlados. Impressoras físicas, preferências e reinstalação exigem teste com o equipamento.
- **Instalação e remoção de programas:** Inventário e fluxos foram verificados em cenários controlados. Instalação, atualização e remoção reais por WinGet/Chocolatey/Scoop, MSI e Store ainda precisam de ensaios por pacote e versão do Windows.
- **Tweaks e configurações avançadas:** Seleção, consulta, backup e restauração foram verificados. O efeito de cada ajuste, remoção e alteração de energia ou segurança ainda precisa de ensaio na edição e versão correspondentes.
- **Limpeza de arquivos e rastros:** Proteções e exclusão de arquivos próprios foram verificadas. Caches e rastros de cada programa real ainda precisam de validação; revise os itens encontrados antes de excluir.
- **Integração com antimalware:** As consultas de processos e os fluxos foram verificados. Detecção de ameaças reais e integração com cada versão dos motores externos ainda precisam de homologação.
- **Mapa de disco:** O índice NTFS foi verificado em discos virtuais. Tempo de leitura e completude em unidades físicas grandes ainda dependem de auditoria com administrador.
- **Reparos do sistema:** Execução e recuperação de falhas precisam de testes adicionais em instalações correspondentes. Consulte os detalhes antes de aplicar.
- **Recursos opcionais:** Consulta e interface verificadas. Instalação/remoção e reinicialização variam por edição e precisam de ensaio específico.
- **Atualização de versão do Windows:** Checagens disponíveis; instalação, migração e contornos de requisitos permanecem em testes e não garantem oferta pelo Windows Update.
- **Limpeza ampliada do Registro:** Backups e rechecagem verificados em cenários controlados. A validade de referências de aplicativos reais precisa de avaliação individual.
- **Alteração de resolução:** Leitura e temporizador verificados; troca/reversão em monitores e drivers diversos ainda requerem validação.
- **Políticas de navegadores:** Regras verificadas em cenários controlados. Efeito com DoH, perfis e diferentes navegadores depende de homologação adicional.
- **Aplicação de DNS:** Aplicação, leitura e restauração passaram em uma compilação anterior no Sandbox. A compilação atual e cenários com VPN, vários adaptadores e DNS seguro ainda precisam de validação administrativa.