import { useMemo } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { financeKeys } from './queryKeys'
import type { CreateCardRequest, UpdateCardRequest } from '../models'
import * as cardsService from '../services/cards.service'
import type { ListCardsParams } from '../services/cards.service'

export function useCards(params: ListCardsParams = {}) {
  return useQuery({
    queryKey: financeKeys.cardList(params),
    queryFn: () => cardsService.listCards(params),
  })
}

export function useCard(id: string) {
  return useQuery({
    queryKey: financeKeys.card(id),
    queryFn: () => cardsService.getCard(id),
    enabled: !!id,
  })
}

export function useCardStatements(id: string) {
  return useQuery({
    queryKey: financeKeys.cardStatements(id),
    queryFn: () => cardsService.getCardStatements(id),
    enabled: !!id,
  })
}

export function useCardInstallmentPlans(id: string) {
  return useQuery({
    queryKey: financeKeys.cardInstallmentPlans(id),
    queryFn: () => cardsService.getCardInstallmentPlans(id),
    enabled: !!id,
  })
}

export function useCardAvailableLimit(id: string) {
  return useQuery({
    queryKey: financeKeys.cardAvailableLimit(id),
    queryFn: () => cardsService.getCardAvailableLimit(id),
    enabled: !!id,
  })
}

/** id → name map for all cards. */
export function useCardNames(): Map<string, string> {
  const { data } = useCards()
  return useMemo(() => {
    const map = new Map<string, string>()
    for (const c of data ?? []) map.set(c.id, c.name)
    return map
  }, [data])
}

function useInvalidateCards() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: financeKeys.cards() })
}

export function useCreateCard() {
  const invalidate = useInvalidateCards()
  return useMutation({
    mutationFn: (body: CreateCardRequest) => cardsService.createCard(body),
    onSuccess: invalidate,
  })
}

export function useUpdateCard() {
  const invalidate = useInvalidateCards()
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateCardRequest }) =>
      cardsService.updateCard(id, body),
    onSuccess: invalidate,
  })
}

export function useDeleteCard() {
  const invalidate = useInvalidateCards()
  return useMutation({
    mutationFn: (id: string) => cardsService.deleteCard(id),
    onSuccess: invalidate,
  })
}

export function useSetCardArchived() {
  const invalidate = useInvalidateCards()
  return useMutation({
    mutationFn: ({ id, archived }: { id: string; archived: boolean }) =>
      archived ? cardsService.archiveCard(id) : cardsService.unarchiveCard(id),
    onSuccess: invalidate,
  })
}
