# Próxima etapa: Google Fotos

Nesta entrega, a prioridade escolhida foi OneDrive. A análise individual do Google Fotos ainda não foi implementada.

## Opção mais simples para selecionar itens

Um botão **Selecionar fotos e vídeos** pode usar o seletor oficial Google Photos Picker. O usuário autoriza a conexão e escolhe quais itens compartilhar. O OuO recebe esses itens selecionados, e não uma varredura automática de toda a biblioteca.

O seletor exige uma autorização adicional. A resposta documentada contém nome, tipo e dados como dimensões, mas não fornece o espaço de cota cobrado por foto. A obtenção dos tamanhos dos arquivos selecionados precisa de ensaio próprio; um valor indisponível deve continuar identificado como indisponível.

É o fluxo com menos preparação para analisar um conjunto escolhido. [Seletor oficial e dados disponíveis](https://developers.google.com/photos/picker/guides/media-items) · [Campos de mídia](https://developers.google.com/photos/picker/reference/rest/v1/mediaItems)

## Opção para uma biblioteca exportada

Um botão **Importar Google Takeout** pode ler os arquivos ZIP exportados pelo Google e ordenar fotos e vídeos pelos tamanhos dos arquivos, sem extrair seu conteúdo. Exige criar e baixar a exportação e ter espaço para armazená-la. Exportações divididas precisam ser tratadas como partes da mesma análise.

É a alternativa para analisar uma exportação da biblioteca inteira, com mais preparação inicial. O tamanho exportado pode diferir do espaço cobrado na conta, inclusive por regras de armazenamento e qualidade. [Como exportar os dados do Google](https://support.google.com/accounts/answer/3024190?hl=pt-BR)

## Limite do acesso automático

A Library API do Google Fotos limita a listagem a conteúdo criado pelo próprio aplicativo. A autorização atual de metadados do Drive permite consultar cotas, mas não dá acesso à biblioteca de fotos. [Mudanças oficiais do Google Fotos](https://developers.google.com/photos/support/updates)

O saldo não classificado na cota Google continua sendo somente uma diferença entre totais recebidos. Esses dados não confirmam que ele pertença a backups, Fotos ou outro serviço.
