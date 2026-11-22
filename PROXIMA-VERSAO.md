# Próxima versão do OuO — escopo de 22/11/2026

Base publicada: 1.0.0.107, prévia nuvem r5. Este documento registra todos os pedidos desta rodada, a abordagem e os critérios de conclusão. A produção da candidata 1.1.0 foi autorizada em 22/11/2026, em uma cópia separada da distribuição publicada.

## Situação

- Licença: uso gratuito com código visível e reutilização reservada, incluída na candidata e na tela Sobre.
- Itens 2 a 12 e melhorias 13 a 18 abaixo: em implementação e validação; a situação final será registrada no relatório da candidata.
- Permanecem vigentes as pendências já descritas em STATUS-DO-ESCOPO.md. Recursos sem ensaio suficiente continuam com a indicação de fase de testes.
- Nome aprovado: **Overclock's Utility Optimizer**, mantendo a abreviação **OuO**.
- Não adicionar cadastro ou login para abrir o OuO ou consultar atualizações. O item sugerido de publicação do login Google está fora desta rodada. A consulta de versões usa diretamente o repositório oficial no GitHub; as conexões de nuvem existentes continuam opcionais.

## 1. Licença gratuita com código visível e restritivo

Decisão do proprietário: manter o código visível. A licença própria permite baixar e usar o programa, inclusive na atividade profissional, e restringe o reaproveitamento do código original em outros projetos sem autorização. Respeita os direitos de forks da plataforma, as permissões anteriores e todas as licenças dos componentes. Não modifica retroativamente arquivos de releases publicadas nem elimina cópias anteriores. Texto em LICENSE.md.

## 2. Oferta do driver de sensores na primeira abertura

Depois do aviso inicial e em segundo plano, verificar a instalação do PawnIO pelo serviço e pelos arquivos/versão registrados. Um serviço instalado, mesmo parado, conta como instalação existente; falha de consulta não será tratada como ausência confirmada.

Quando nenhuma versão for encontrada, mostrar uma oferta integrada ao OuO, com **Instalar agora**, **Mais tarde** e explicação da leitura de temperaturas. Guardar a decisão para não repetir a oferta a cada abertura. A instalação só começa após o clique do usuário, com o instalador oficial assinado e a elevação necessária; depois, conferir a versão instalada e reiniciar a leitura dos sensores.

Antes de ativar a oferta, atualizar a preparação do instalador: a distribuição atual contém PawnIO 2.1.0; o fornecedor já publica 2.2.0. Há relato no projeto Libre Hardware Monitor sobre incompatibilidade do 2.1.0 no Windows 10 LTSC 2019. Verificar assinatura, hash oficial e compatibilidade por versão/arquitetura do Windows. Não instalar a edição sem assinatura.

**Conclusão:** testar ausência, versão existente, consulta negada, cancelamento do UAC, instalação concluída e instalador incompatível; a recusa no aviso inicial continua encerrando o programa sem inicializar hardware.

## 3. Limpeza de drivers de vídeo e áudio

Criar uma área **Drivers → Limpeza de vídeo e áudio**, com inventário do fabricante, dispositivo e pacote selecionado. O fluxo deve mostrar o que será afetado, exportar os pacotes selecionados e orientar a recuperação antes da remoção. A remoção nativa deve usar as interfaces suportadas do Windows/PnPUtil, com verificação dos dispositivos que compartilham o mesmo pacote.

Para limpeza profunda, a proposta é uma integração guiada com o DDU oficial, obtido do fornecedor quando solicitado. Seu escopo publicado inclui GPUs NVIDIA, AMD e Intel e limpeza básica de áudio Realtek. Preservar créditos e termos; não redistribuir o DDU sem permissão nem apresentar a limpeza nativa como se fosse o motor DDU.

Oferecer orientações de Modo de Segurança, reinicialização e BitLocker quando aplicáveis. Não alterar boot automaticamente nem criar exclusões de antivírus. A limpeza deve exigir seleção explícita e uma revisão final do dispositivo/pacote; não executar na abertura ou como manutenção periódica. Manter identificação de recurso em testes até validar remoção, reinício e reinstalação em ambiente descartável.

**Conclusão:** preservar drivers básicos do Windows e pacotes de outros dispositivos; testar pacote compartilhado, backup insuficiente, cancelamento e reinstalação. Não usar o computador de trabalho para ensaios destrutivos.

## 4. Reorganização dos menus

Agrupar por finalidade e retirar duplicações e atalhos internos que apenas repetem outra tela. Proposta de organização:

| Grupo | Conteúdo |
| --- | --- |
| Início | Visão geral e assistente de recomendações |
| Desempenho | Memória, tweaks e inicialização |
| Armazenamento | Discos/mapa, nuvem, limpeza de arquivos/registro e clonagem |
| Programas | Instalar/atualizar, Office, remover programas e bibliotecas |
| Drivers | Atualizações e limpeza de vídeo/áudio |
| Windows e diagnóstico | Processos, estabilidade/eventos, reparos, atualização do Windows, proteção/recuperação e impressão |
| Rede e navegação | Conexões, DNS, bloqueios, navegadores e extensões |
| Configurações | Idioma/aparência, histórico, Sobre e licença |

Manter busca de funções, ícones próprios e o estado das telas ao navegar. Usar abas internas para variações da mesma função.

**Conclusão:** mapear todas as funções atuais, confirmar que nenhuma desapareceu ou ficou duplicada, e conferir navegação nos dois idiomas.

## 5. Progresso com porcentagem confiável

Usar totais reais quando conhecidos: bytes recebidos no download, arquivos processados e programas/etapas de uma fila. Mostrar atividade atual, progresso e contagem em linguagem simples. Estimativas por etapas devem estar identificadas como aproximação. Quando a ferramenta não fornecer um total, mostrar a etapa e os itens processados, sem inventar uma porcentagem exata por temporizador.

Para tarefas simultâneas, exibir o progresso de cada operação e permitir identificar qual delas corresponde à barra principal. Falhas e cancelamentos não devem aparecer como sucesso ao alcançar 100%.

**Conclusão:** conferir progresso monotônico dentro de cada etapa, operações sem total, várias operações, mudança de tela, cancelamento e erro.

## 6. Barra de ações dos tweaks sempre acessível

Fixar a barra **Aplicar / Checar / Restaurar** na área visível da página, com indicação da seleção, enquanto apenas a lista de ajustes rola. Ela deve respeitar o tamanho da janela, o tema e a mudança de idioma, sem cobrir opções nem criar botões duplicados.

**Conclusão:** aplicar e restaurar a seleção após rolar ao fim da página, filtrar categorias, navegar e retornar, e usar janela estreita/escala de exibição elevada.

## 7. Desempenho do próprio OuO

Medir tempo até a janela utilizável, consumo de memória, CPU em repouso e navegação antes/depois. Adiar consultas pesadas até a tela precisar delas; fazer detecção inicial de navegadores de forma leve e assíncrona. Reaproveitar inventários, ícones e resultados, reduzir recriação de controles, virtualizar listas grandes e limitar atualizações visuais muito frequentes.

Revisar o custo do monitoramento contínuo e ajustar a frequência quando o aplicativo estiver ocioso ou na bandeja. Não aplicar tweaks de desempenho do Windows sem uma ação do usuário para melhorar o tempo de abertura do próprio aplicativo.

**Conclusão:** comparar medições com a versão de base e testar uso com poucos núcleos/memória, listas grandes e várias tarefas, sem prometer uma redução percentual antes de medir.

## 8. Alinhamento e responsividade do aplicativo inteiro

A implementação atual de CardColumns distribui cada cartão na coluna mais curta, em formato de mosaico. Isso contribui para inícios de linhas em alturas diferentes, como no recorte enviado. Nas telas de formulários, usar colunas estáveis por grupo: cada cartão permanece na mesma coluna e o próximo cartão acompanha o anterior ao expandir um relatório, sem saltar para outra coluna nem reservar uma linha inteira vazia. Usar linhas alinhadas para controles e resumos, larguras responsivas e espaçamentos consistentes.

Padronizar margens, títulos, descrições, botões e campos. Relatórios recolhidos não devem reservar grandes retângulos vazios. Rever todas as telas, inclusive estados com resultados, filas e avisos expandidos.

**Conclusão:** conferir janelas grandes e pequenas, temas claro/escuro/compacto, português/inglês e escalas de 100%, 125%, 150% e 200%, sem recorte de controles ou sobreposição da barra de título.

## 9. Seletor visual de horário

Criar um seletor de horário com relógio em janela integrada ao tema, seleção de hora e minuto e entrada direta HH:mm. Aplicar aos campos que representam horário do dia, como backup do Registro e ponto de restauração. Campos de duração/intervalo continuam representando duração, sem conversão indevida para horário.

**Conclusão:** validar 00:00/23:59, horário inválido, teclado, acessibilidade, cancelamento e preservação do horário local nas agendas.

## 10. Limpar o log exibido em detalhes técnicos

Adicionar **Limpar log exibido** ao componente compartilhado de resultados e aos detalhes que não o utilizam. O botão aparece junto ao conteúdo expandido e limpa o relatório daquela operação, com estado vazio consistente. Relacionar a ação à operação correta para evitar que um retorno antigo repovoe o relatório depois de outra tarefa.

Essa ação não apaga os eventos do Windows, os backups ou o histórico de recuperação. Eventual exclusão de arquivos permanentes de diagnóstico deve ser uma função identificada separadamente.

**Conclusão:** conferir relatórios vazios, resultado de sucesso/erro, tarefas em andamento, mudança de tela e os dois idiomas.

## 11. Link do fabricante nos discos

Mostrar, junto aos detalhes do disco selecionado, um link para o suporte oficial do fabricante, usando fabricante/modelo para resolver a marca. Priorizar identificação confiável e indicar quando houver somente uma inferência pelo modelo. O link acompanha a troca do disco.

Quando o Windows não identificar a marca, informar a limitação e oferecer pesquisa pelo modelo, sem fabricar um domínio de suporte e sem enviar número de série. Manter uma lista de domínios oficiais para as marcas reconhecidas.

**Conclusão:** testar SATA/NVMe/USB/RAID, marca genérica ou desconhecida, modelos com caracteres especiais e troca de seleção.

## 12. Mais navegadores e detecção inicial de extensões

Na abertura do OuO, detectar navegadores instalados em segundo plano, consultando caminhos e cadastros de instalação, sem ler histórico ou cookies. Mostrar os detectados na tela de proteção e permitir atualizar a lista sem reiniciar o programa.

Ampliar Chrome, Edge, Brave e Firefox para Vivaldi, Opera/Opera GX, Chromium e variantes compatíveis, distinguindo família, canal e instalação por usuário ou máquina. Verificar cada mecanismo de instalação antes de anunciá-lo como automático. Se um navegador ou uma extensão não oferecer suporte adequado, apresentar a instalação assistida pela loja oficial.

Usar uBlock Origin ou uBlock Origin Lite conforme a compatibilidade do navegador e conferir IDs/lojas pelos projetos oficiais. Manter o WOT como uma escolha explícita e revisar suas permissões e disponibilidade em cada loja. Preservar políticas de organizações já existentes, oferecer desfazer as regras criadas pelo OuO e diferenciar regra aplicada de extensão efetivamente instalada. A detecção na abertura não autoriza instalar extensões silenciosamente sem a seleção do usuário.

**Conclusão:** testar navegadores ausentes, instalações em caminhos alternativos, variantes, políticas conflitantes, permissões, rede indisponível e disponibilidade real de cada extensão. Novos conectores sem ensaio completo permanecem identificados como em testes.

## 13. Histórico com reversão por operação

Agrupar alterações do Registro feitas pela mesma operação, apresentar data/resultado e restaurar o conjunto selecionado. Rechecar o estado final e alterações posteriores antes da reversão. Preservar a restauração individual para registros antigos sem estado final verificado. A reversão não promete recuperar arquivos excluídos ou desfazer qualquer instalador externo.

**Conclusão:** validar valores tipados, operações com mais de um valor, escrita repetida no mesmo valor, conflito externo e ordem de restauração entre operações. Manter os resultados persistentes ao reabrir o OuO.

## 14. Relatar problema a partir de uma falha

Acrescentar ação junto dos resultados com erro e na janela de atividades. Gerar texto com versão, operação e mensagem, removendo caminhos, e-mails e credenciais reconhecíveis. Permitir revisar, editar, copiar e salvar; não enviar automaticamente para terceiros.

**Conclusão:** conferir remoção de dados reconhecíveis, resultado sem erro, cancelamento e relatório grande, com apresentação integrada ao tema.

## 15. Atualizador do próprio OuO pelo GitHub

Consultar as releases públicas de CarvalhoRonan/OuO sem conta ou autenticação. Oferecer canal estável ou estável/prévia, conferir metadados, tamanho e SHA-256 do pacote oficial, guardar a versão anterior e preparar a nova em pasta separada. Preservar configurações do perfil do Windows e informar como abrir a nova cópia depois de fechar a atual.

**Conclusão:** validar canais, versões numéricas, release vazia/rascunho, falha de rede, hash/tamanho divergente, cancelamento, caminhos maliciosos no ZIP e recuperação entre duas distribuições completas. A troca completa fica em fase de testes até o ensaio correspondente.

## 16. Reinicialização pendente

Consolidar solicitações do Windows e resultados explícitos das operações do OuO, persistindo os motivos durante a mesma inicialização. Apresentar o que aguarda reinício, permitir adiar e oferecer reinicialização revisada pelo usuário quando não houver tarefas em andamento.

**Conclusão:** conferir estados ausentes/presentes/inacessíveis, limpeza dos avisos de uma inicialização anterior, resultados cancelados e ação de cancelar uma reinicialização agendada pelo OuO. Nenhum reinício é disparado durante os testes no computador de trabalho.

## 17. Comparação antes e depois

Registrar duração, espaço disponível na unidade do Windows e RAM disponível no início/fim. Exibir resultados na janela de atividades e no histórico. Identificar que as variações podem incluir outros programas e não equivalem a ganho permanente de desempenho.

**Conclusão:** persistência, ausência de dados, falha/cancelamento e operações simultâneas sem atribuir toda variação de recursos ao OuO.

## 18. Acessibilidade e identificação de estados

Acrescentar texto e símbolos aos estados, nomes acessíveis nos controles, foco de teclado visível e atalhos para buscar funções e alternar áreas. Preservar as cores semânticas e conferir mudanças de idioma/tema/escala.

**Conclusão:** navegação por teclado, foco visível e estados distinguíveis sem depender somente de verde/vermelho. Leitor de tela e múltiplos monitores continuam exigindo ensaio próprio quando não disponíveis.

## Critérios de liberação por recurso

Cada relatório deve separar testes automatizados, ensaios reais e cenários pendentes. Verificar instalação limpa, atualização da versão anterior, cancelamento, falha de conexão e recuperação. MSI, Office e reparos do sistema mantêm prioridade nas próximas rodadas administrativas e conservam o aviso de testes enquanto seus fluxos reais não forem homologados. Um resultado em testes controlados não qualifica automaticamente hardware físico ou outra edição do Windows.

## Referências consultadas

- GitHub, licenciamento e forks: https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository
- Mozilla, MPL e componentes em produtos proprietários: https://www.mozilla.org/en-US/MPL/2.0/FAQ/
- PawnIO oficial e versão: https://pawnio.eu/ e https://github.com/namazso/PawnIO.Setup/releases/tag/2.2.0
- Relato de compatibilidade a verificar no preparo do driver: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/issues/2483
- DDU, guia e suporte: https://www.wagnardsoft.com/content/How-use-Display-Driver-Uninstaller-DDU-Guide-Tutorial
- DDU, atribuição/distribuição: https://www.wagnardsoft.com/forums/viewtopic.php?t=5700
- Microsoft PnPUtil: https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-command-syntax
- Chrome, políticas de extensões: https://support.google.com/chrome/a/answer/7532015
- Firefox, ExtensionSettings: https://firefox-admin-docs.mozilla.org/reference/policies/extensionsettings/
- Brave, políticas: https://support.brave.app/hc/en-us/articles/360039248271-Group-Policy
- Vivaldi Enterprise: https://help.vivaldi.com/developers/enterprise/vivaldi-enterprise/
- uBlock Origin e uBlock Origin Lite: https://github.com/gorhill/uBlock e https://github.com/uBlockOrigin/uBOL-home
