# OuO 1.1.0 — Overclock's Utility Optimizer

Prévia 1 • 22/11/2026 • Overclock Informática.

Esta entrega reúne os 12 pedidos registrados e as seis melhorias adicionais aprovadas. A publicação do login Google ficou fora desta rodada. O OuO abre sem conta e verifica suas atualizações no repositório público CarvalhoRonan/OuO. Conectar uma conta de nuvem continua sendo uma escolha separada na análise de armazenamento.

| Item | O que mudou e onde usar |
| --- | --- |
| 1. Licença | Uso gratuito com código visível e reutilização reservada. LICENSE.md acompanha o pacote e pode ser aberto em Sobre. Componentes de terceiros mantêm seus próprios termos. |
| 2. Sensores | A primeira abertura consulta a instalação do PawnIO. Quando ausente, oferece a preparação; serviço instalado, mesmo parado, impede oferta indevida. O instalador oficial 2.2.0 foi conferido por assinatura e SHA-256. Instalação real continua em testes. |
| 3. Limpeza de vídeo/áudio | Em Drivers, selecione pacotes OEM assinados. O OuO relê o inventário, exporta backup e usa o PnPUtil sem remoção forçada. Pacotes Microsoft e compartilhados com outras classes são preservados. Há acesso ao guia oficial do DDU para limpeza profunda; o motor DDU não é distribuído nem executado pelo OuO. |
| 4. Menus | Oito grupos: Início, Desempenho, Armazenamento, Programas, Drivers, Windows e diagnóstico, Rede e navegação, Configurações. Busca e estado das páginas foram preservados. |
| 5. Progresso | Cada atividade mostra sua etapa. Totais conhecidos usam contagem ou bytes reais; downloads usam unidades legíveis. Ferramentas que não informam total continuam sem porcentagem inventada. Atividades e resultados reúne operações simultâneas. |
| 6. Tweaks | Aplicar, Checar, Restaurar e as ações de seleção permanecem visíveis enquanto a lista rola. |
| 7. Desempenho do OuO | CPU usa contadores nativos. Consultas pesadas de GPU são reduzidas fora da visão geral; a atualização fica menos frequente em outras telas. A alternativa WMI dos sensores tem intervalo e os navegadores usam inventário em cache. Não há promessa de ganho percentual sem medição equivalente. |
| 8. Layout | Colunas estáveis mantêm cada cartão em seu grupo ao expandir resultados, sem redistribuí-lo para a coluna mais curta. Controles e resumos mantêm linhas alinhadas e se adaptam à largura. |
| 9. Horários | Agendas de Registro e restauração têm relógio de 24 horas, seletores de hora/minuto e entrada HH:mm pelo teclado. Alt+↓ abre o relógio a partir do campo. |
| 10. Logs | Ver detalhes técnicos inclui Limpar log exibido. Limpa somente o relatório visível; eventos do Windows, backups e histórico não são apagados. |
| 11. Fabricantes | Os detalhes dos discos incluem suporte oficial para 14 marcas reconhecidas. Marca desconhecida oferece pesquisa pelo modelo, sem enviar número de série. |
| 12. Navegadores | Inventário inicial de 14 definições, incluindo canais de Chrome/Edge/Firefox, Chromium, Vivaldi, Opera/GX e LibreWolf. Políticas automáticas são usadas onde implementadas; novos conectores continuam em testes. Navegadores sem automação validada usam instalação assistida na loja oficial. WOT permanece opcional. |
| 13. Desfazer por operação | Histórico agrupa alterações do Registro e permite restaurar o conjunto selecionado. Confere mudanças externas e operações posteriores antes de restaurar. Registros antigos mantêm o caminho de restauração individual. |
| 14. Relatar problema | Falhas oferecem relatório revisável com versão, operação e mensagem. Caminhos, e-mails e credenciais reconhecíveis são removidos. Permite editar, copiar e salvar; nada é enviado automaticamente. |
| 15. Atualização do OuO | Sobre consulta o GitHub sem login. Há canais estável/prévia, novidades, verificação de tamanho e SHA-256, cópia da versão anterior e preparo em pasta separada. A ativação da nova cópia ainda requer fechar a atual e abrir a pasta preparada. |
| 16. Reinicialização | Consolida solicitações do Windows e resultados explícitos do OuO. Mostra motivos, permite adiar e oferece agendar/cancelar reinício após revisão. Nenhum reinício foi disparado na validação desta entrega. |
| 17. Antes/depois | Atividades e Histórico mostram duração, espaço livre na unidade Windows e RAM disponível. As variações podem incluir outros programas; não são apresentadas como ganho permanente de desempenho. |
| 18. Acessibilidade | Estados incluem símbolos e texto, controles possuem nomes acessíveis e botões mostram foco. Ctrl+L busca uma função; F6 alterna as áreas de navegação e conteúdo. |

O nome completo passou a **Overclock's Utility Optimizer**, mantendo **OuO** como abreviação e OuO.exe como executável. As preferências existentes continuam no mesmo perfil.

Consulte HOMOLOGACAO-1.1.0.md e CRITERIOS-DE-LIBERACAO.json. Recursos sem ensaio suficiente mantêm **Recurso em fase de testes**. A distribuição continua sem assinatura digital, conforme a decisão anterior do proprietário.
