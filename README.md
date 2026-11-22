# OuO — Overclock's Utility Optimizer

Utilitário portátil de manutenção e diagnóstico do Windows, desenvolvido pela **Overclock Informática**.

**Versão atual: 1.1.0 — prévia 1 (arquivo 1.1.0.0).** Recursos ainda sem qualificação completa exibem **Recurso em fase de testes**. A distribuição permanece sem assinatura de código.

## Download

[**Baixar OuO 1.1.0 — pacote completo**](https://github.com/CarvalhoRonan/OuO/releases/download/v1.1.0-preview1/OuO-1.1.0-previa1.zip)

Extraia todo o ZIP e abra **OuO.exe**. Não é preciso criar conta ou fazer login para abrir o programa ou consultar suas atualizações no GitHub. Conexões de nuvem são opcionais e separadas.

## Novidades

Os 12 pedidos e as seis melhorias adicionais aprovadas estão em [NOVIDADES-1.1.0.md](NOVIDADES-1.1.0.md): nome novo, licença, preparação dos sensores, limpeza de vídeo/áudio com backup, menus reorganizados, progresso por tarefa, barra fixa dos tweaks, layout, relógio, limpar logs, suporte dos fabricantes e mais navegadores. Também inclui reversão por operação, relatórios de falha, atualização pelo GitHub, reinicialização pendente, comparação antes/depois e acessibilidade.

A consulta de versões fica em **Configurações → Sobre → Atualização do OuO**, com canais estável e prévia. O pacote é conferido pelo tamanho e SHA-256, com backup da cópia atual e preparo em outra pasta. A ativação e a recuperação entre versões ainda estão em testes. A limpeza nativa de drivers não é o motor DDU; o guia oficial está disponível no módulo.

## Validação

- 407 verificações automatizadas e 26 telas aprovadas.
- 38 verificações de aceitação aprovadas.
- Dez minutos e 8.242 trocas de tela, com memória gerenciada e identificadores de recursos acompanhados.
- Consulta pública do GitHub, download real, integridade, backup e extração verificados sem ativar a cópia baixada.
- Pasta e ZIP desta entrega verificados pelo Defender; resultados se limitam à rodada registrada.

**25 grupos continuam em testes.** Consulte [HOMOLOGACAO-1.1.0.md](HOMOLOGACAO-1.1.0.md), [CRITERIOS-DE-LIBERACAO.json](CRITERIOS-DE-LIBERACAO.json) e [STATUS-DO-ESCOPO.md](STATUS-DO-ESCOPO.md). MSI, Office, reparos e operações administrativas em hardware real conservam suas pendências. Esta prévia não qualifica o produto inteiro como estável.

SHA-256 do ZIP:

```text
8ACCB35B77B0CD5CBBA5F757C61B436C09EC61C480B12435269915361752D146
```

## Código e licença

O código está em [source](source). O repositório reúne as fontes de texto; o pacote da release inclui dependências, imagens e fontes originais dos componentes. `ARQUIVOS-SHA256.csv` descreve os arquivos distribuídos. O script `Compilar.ps1` usa o compilador .NET Framework do Windows; o parâmetro opcional `-GoogleOAuthFile` inclui somente o cadastro do aplicativo de computador, nunca autorizações de contas. O cadastro integrado do Google continua em modo de testes; sua publicação ficou fora desta rodada.

[Licença de Uso Gratuito e Código Visível](LICENSE.md): uso gratuito, inclusive profissional, e compartilhamento gratuito do pacote oficial íntegro. Reutilização do código próprio e versões derivadas exigem autorização, respeitados os direitos legais e da plataforma. Os componentes de terceiros mantêm suas licenças; veja [THIRD-PARTY.md](THIRD-PARTY.md), [licenses](licenses) e [third-party-source](third-party-source).

[Escopo desta versão](PROXIMA-VERSAO.md) · [Overclock Informática — contato e links](https://linkme.bio/overclockinformatica) · [Instagram](https://www.instagram.com/oc.informatica/).
